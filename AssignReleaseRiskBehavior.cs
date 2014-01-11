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
        List<Project> projects;
        bool inverted;
        bool changeImpact = false;
        bool initializationOK = false;
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
            string invert = GetParameter("InvertedMatch");
            if (invert != null)
                inverted = invert.ToLower().Equals("yes");

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
            initializationOK = false;
            projects = HPMUtilities.FindProjects(projectName, inverted);
            if (projects.Count == 0)
                throw new ArgumentException("Could not find any matching project:" + projectName);
            trackingColumn = projects[0].ProductBacklog.GetCustomColumn(trackingColumnName);
            if (trackingColumn == null)
                throw new ArgumentException("Could not find custom column in product backlog:" + trackingColumnName);
            initializationOK = true;
            DoUpdate();
        }

        public override string Title
        {
            get { return title; }
        }

        private void DoUpdate()
        {
            if (initializationOK)
            {
                foreach (Project project in projects)
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

                        // Get the column in the actual project
                        HPMProjectCustomColumnsColumn actualCustomColumn = project.ProductBacklog.GetCustomColumn(trackingColumn.m_Name);

                        AssignReleaseRisk(completedItems, riskValue, 0, actualCustomColumn);
                        AssignReleaseRisk(remainingItemsByReleaseRisk[0], riskValue, 1, actualCustomColumn);
                        AssignReleaseRisk(remainingItemsByReleaseRisk[1], riskValue, 2, actualCustomColumn);
                        AssignReleaseRisk(remainingItemsByReleaseRisk[2], riskValue, 3, actualCustomColumn);
                    }
                }
            }
        }

        private void AssignReleaseRisk(List<ProductBacklogItem> items, string[] riskValue, int index, HPMProjectCustomColumnsColumn actualTrackingColumn)
        {
            foreach (ProductBacklogItem item in items)
            {
                if (index != 0)
                {
                    if ((EHPMTaskStatus)item.AggregatedStatus.Value == EHPMTaskStatus.Blocked && elevateBlockedItems)
                    {
                        item.SetCustomColumnValue(actualTrackingColumn, riskValue[3]);
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
                        item.SetCustomColumnValue(actualTrackingColumn, riskValue[iind]);
                    }
                }
                else
                    item.SetCustomColumnValue(actualTrackingColumn, riskValue[index]);
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
