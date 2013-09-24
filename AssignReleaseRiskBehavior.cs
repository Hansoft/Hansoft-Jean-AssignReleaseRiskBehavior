using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;


using HPMSdk;
using Hansoft.ObjectWrapper;
using Hansoft.ObjectWrapper.CustomColumnValues;

using Hansoft.Jean.Behavior;

namespace Hansoft.Jean.Behavior.AssignReleaseRiskBehavior
{
    public class AssignReleaseRiskBehavior : AbstractBehavior
 {
        string projectName;
        string trackingColumnName;
        Project project;
        bool changeImpact = false;
        string pointsOrDays;
        string findQuery;
        double lowRiskLimit;
        double highRiskLimit;
        string[] riskValue;
        bool elevateBlockedItems;
        bool considerRiskColumn;
        HPMProjectCustomColumnsColumn trackingColumn;
        string title;

        public AssignReleaseRiskBehavior(XmlElement configuration)
            : base(configuration)
        {
            projectName = GetParameter("HansoftProject");
            trackingColumnName = GetParameter("TrackingColumn");
            pointsOrDays = GetParameter("PointsOrDays");
            findQuery = GetParameter("Find");
            lowRiskLimit = double.Parse(GetParameter("LowRiskLimit"));
            highRiskLimit = double.Parse(GetParameter("HighRiskLimit"));
            string riskValues = GetParameter("RiskValues");
            riskValue = riskValues.Split(',');
            for (int i = 0; i < riskValue.Length; i += 1)
                riskValue[i] = riskValue[i].Trim();
            title = "AssignReleaseRiskBehavior: " + configuration.InnerText;
            elevateBlockedItems = GetParameter("ElevateBlockedItems").Equals("Yes");
            considerRiskColumn = GetParameter("ConsiderRiskColumn").Equals("Yes");
        }

        public override void Initialize()
        {
            project = HPMUtilities.FindProject(projectName);
            if (project == null)
                throw new ArgumentException("Could not find project:" + projectName);
            trackingColumn = project.ProductBacklog.GetCustomColumn(trackingColumnName);
            if (trackingColumn == null)
                throw new ArgumentException("Could not find custom column in product backlog:" + trackingColumnName);
            DoUpdate();
        }

        public override string Title
        {
            get { return title; }
        }

        private void DoUpdate()
        {
            List<Task> releases = project.Schedule.Find(findQuery);
            DateTime start = DateTime.Now;
            foreach (Release release in releases)
            {
                List<ProductBacklogItem>[] remainingItemsByReleaseRisk;
                List<ProductBacklogItem> completedItems = release.CompletedItems;
                if (pointsOrDays.Equals("Points"))
                    remainingItemsByReleaseRisk = release.GetRemainingItemsByRiskPoints(lowRiskLimit, highRiskLimit);
                else
                    remainingItemsByReleaseRisk = release.GetRemainingItemsByRiskEstimatedDays(lowRiskLimit, highRiskLimit);

                AssignReleaseRisk(completedItems, riskValue, 0);
                AssignReleaseRisk(remainingItemsByReleaseRisk[0], riskValue, 1);
                AssignReleaseRisk(remainingItemsByReleaseRisk[1], riskValue, 2);
                AssignReleaseRisk(remainingItemsByReleaseRisk[2], riskValue, 3);
            }
        }

        private void AssignReleaseRisk(List<ProductBacklogItem> items, string[] riskValue, int index)
        {
            foreach (ProductBacklogItem item in items)
            {
                if (index != 0)
                {
                    if ((EHPMTaskStatus)item.AggregatedStatus.Value == EHPMTaskStatus.Blocked && elevateBlockedItems)
                    {
                        item.SetCustomColumnValue(trackingColumn, riskValue[3]);
                    }
                    else
                    {
                        int iind = index;
                        if (considerRiskColumn)
                        {
                            if ((EHPMTaskRisk)item.Risk.Value == EHPMTaskRisk.High)
                                iind = 3;
                            else if ((EHPMTaskRisk)item.Risk.Value == EHPMTaskRisk.Medium)
                                iind = Math.Max(2, index);
                        }
                        item.SetCustomColumnValue(trackingColumn, riskValue[iind]);
                    }
                }
                else
                    item.SetCustomColumnValue(trackingColumn, riskValue[index]);
            }
        }

        public override void OnBeginProcessBufferedEvents(EventArgs e)
        {
            changeImpact = false;
        }

        public override void OnEndProcessBufferedEvents(EventArgs e)
        {
            if (BufferedEvents && changeImpact)
                DoUpdate();
        }


        public override void OnTaskChange(TaskChangeEventArgs e)
        {
            //            if (Task.GetTask(e.Data.m_TaskID).MainProjectID.m_ID == project.UniqueID.m_ID)
            //            {
            if (!BufferedEvents)
                DoUpdate();
            else
                changeImpact = true;
            //            }
        }

        public override void OnTaskChangeCustomColumnData(TaskChangeCustomColumnDataEventArgs e)
        {
            //            if (Task.GetTask(e.Data.m_TaskID).MainProjectID.m_ID == project.UniqueID.m_ID)
            //            {
            if (!BufferedEvents)
                DoUpdate();
            else
                changeImpact = true;
            //            }
        }

        public override void OnTaskCreate(TaskCreateEventArgs e)
        {
            //            if (e.Data.m_ProjectID.m_ID == projectView.UniqueID.m_ID)
            //            {
            if (!BufferedEvents)
                DoUpdate();
            else
                changeImpact = true;
            //            }
        }

        public override void OnTaskDelete(TaskDeleteEventArgs e)
        {
            if (!BufferedEvents)
                DoUpdate();
            else
                changeImpact = true;
        }

        public override void OnTaskMove(TaskMoveEventArgs e)
        {
            //            if (e.Data.m_ProjectID.m_ID == projectView.UniqueID.m_ID)
            //            {
            if (!BufferedEvents)
                DoUpdate();
            else
                changeImpact = true;
            //            }
        }
    }
}
