﻿using CoralTime.Common.Exceptions;
using CoralTime.Common.Helpers;
using CoralTime.DAL.ConvertModelToView;
using CoralTime.DAL.Models;
using CoralTime.ViewModels.Reports;
using CoralTime.ViewModels.Reports.Request.Grid;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using static CoralTime.Common.Constants.Constants;

namespace CoralTime.BL.Services.Reports.DropDownsAndGrid
{
    public partial class ReportsService
    {
        #region Get DropDowns and Grid. Filtration By / Grouping By: None, Projects, Users, Dates, Clients.

        public ReportsTotalGridTimeEntryView ReportsGridGroupByNone(ReportsGridView reportsGridData)
        {
            var reportsGridTimeEntry = new ReportsTotalGridTimeEntryView
            {
                ReportsGridView = new List<ReportTotalForGridTimeEntryView>
                {
                    new ReportTotalForGridTimeEntryView
                    {
                        Items = new List<ReportsGridItemsView>()
                    }
                }
            };

            var timeEntriesForGrouping = GetTimeEntriesForGrouping(reportsGridData);
            if (!timeEntriesForGrouping.Any())
            {
                return reportsGridTimeEntry;
            }

            var timeEntriesGroupByNone = timeEntriesForGrouping.ToList()
                .GroupBy(x => x.Id)
                .ToDictionary(key => key.Key, key => key.OrderBy(value => value.Date).AsEnumerable());

            var result = reportsGridTimeEntry.GetViewReportsTotalGridTimeEntries(timeEntriesGroupByNone, Mapper);

            return result;
        }

        public ReportsTotalGridProjectsView GetGroupingReportsGridByProjects(ReportsGridView reportsGridData)
        {
            var reportsTotalGridProjectsView = new ReportsTotalGridProjectsView
            {
                ReportsGridView = new List<ReportTotalForGridProjectView>
                {
                    new ReportTotalForGridProjectView
                    {
                        Items = new List<ReportsGridItemsView>()
                    }
                }
            };

            var timeEntriesForGrouping = GetTimeEntriesForGrouping(reportsGridData);
            if (!timeEntriesForGrouping.Any())
            {
                return reportsTotalGridProjectsView;
            }

            var timeEntriesGroupByProjects = timeEntriesForGrouping.ToList()
                .GroupBy(i => i.Project)
                .OrderBy(x => x.Key.Name)
                .ToDictionary(key => key.Key, key => key.OrderBy(value => value.Date).AsEnumerable());

            var result = reportsTotalGridProjectsView.GetViewReportsTotalGridProjects(timeEntriesGroupByProjects, Mapper);

            return result;
        }

        // TODO Check empty list how it was working in frimt end 
        public ReportsTotalGridMembersView GetGroupingReportsGridByUsers(ReportsGridView reportsGridData)
        {
            var timeEntriesGroupByUsers = GetTimeEntriesForGrouping(reportsGridData).ToList()
                .GroupBy(i => i.Member)
                .OrderBy(x => x.Key.FullName)
                .ToDictionary(key => key.Key, key => key.OrderBy(value => value.Date).AsEnumerable());

            var reportsGridUsers = new ReportsTotalGridMembersView().GetViewReportsTotalGridUsers(timeEntriesGroupByUsers, Mapper);

            return reportsGridUsers;
        }

        public ReportsTotalGridByDatesView GetGroupingReportsGridByDates(ReportsGridView reportsGridData)
        {
            var reportsGridDates = new ReportsTotalGridByDatesView
            {
                ReportsGridView = new List<ReportTotalForGridDateView>
                {
                    new ReportTotalForGridDateView
                    {
                        Items = new List<ReportsGridItemsView>()
                    }
                }
            };

            var timeEntriesForGrouping = GetTimeEntriesForGrouping(reportsGridData);
            if (!timeEntriesForGrouping.Any())
            {
                return reportsGridDates;
            }

            var timeEntriesGroupByDate = timeEntriesForGrouping.ToList()
                .GroupBy(i => i.Date)
                .ToDictionary(key => key.Key, key => key.AsEnumerable());

            var result = reportsGridDates.GetViewReportsTotalGridDatess(timeEntriesGroupByDate, Mapper);

            return result;
        }

        public ReportsTotalGridClients GetGroupingReportsGridByClients(ReportsGridView reportsGridData)
        {
            var reportsGridClients = new ReportsTotalGridClients
            {
                ReportsGridView = new List<ReportsTotalForGridClientView>
                {
                    new ReportsTotalForGridClientView
                    {
                        Items = new List<ReportsGridItemsView>()
                    }
                }
            };

            var timeEntriesForGrouping = GetTimeEntriesForGrouping(reportsGridData);
            if (!timeEntriesForGrouping.Any())
            {
                return reportsGridClients;
            }

            var timeEntriesGroupByClients = timeEntriesForGrouping.ToList()
                .GroupBy(i => i.Project.Client == null ? CreateWithOutClientInstance() : i.Project.Client)
                .OrderBy(x => x.Key.Name)
                .ToDictionary(key => key.Key, key => key.OrderBy(value => value.Date).AsEnumerable());

            var result = reportsGridClients.GetViewReportsTotalGridClients(timeEntriesGroupByClients, Mapper);

            return result;
        }

        #endregion

        #region Get DropDowns and Grid. Filtration By / Grouping By: None, Projects, Users, Dates, Clients. (Common methods)

        private IQueryable<TimeEntry> GetTimeEntriesForGrouping(ReportsGridView reportsGridData)
        {
            var currentMember = Uow.MemberRepository.LinkedCacheGetByName(InpersonatedUserName);

            if (currentMember == null)
            {
                throw new CoralTimeEntityNotFoundException($"Member with userName = {InpersonatedUserName} not found.");
            }

            if (!currentMember.User.IsActive)
            {
                throw new CoralTimeEntityNotFoundException($"Member with userName = {InpersonatedUserName} is not active.");
            }

            CommonHelpers.SetRangeOfWeekByDate(out var weekStart, out var weekEnd, DateTime.Now);

            DateFrom = reportsGridData.CurrentQuery?.DateFrom ?? weekStart;
            DateTo = reportsGridData.CurrentQuery?.DateTo ?? weekEnd;

            // By Dates (default grouping, i.e. "Group by None"; direct order).
            var timeEntriesByDateOfUser = GetTimeEntryByDate(currentMember, DateFrom, DateTo);

            // By Projects.
            if (reportsGridData.CurrentQuery?.ProjectIds != null && reportsGridData.CurrentQuery.ProjectIds.Length > 0)
            {
                CheckAndSetIfInFilterChooseSingleProject(reportsGridData, timeEntriesByDateOfUser);

                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridData.CurrentQuery.ProjectIds.Contains(x.ProjectId));
            }

            // By Members.
            if (reportsGridData.CurrentQuery?.MemberIds != null && reportsGridData.CurrentQuery.MemberIds.Length > 0)
            {
                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridData.CurrentQuery.MemberIds.Contains(x.MemberId));
            }

            // By Clients that has Projects.
            if (reportsGridData.CurrentQuery?.ClientIds != null && reportsGridData.CurrentQuery.ClientIds.Length > 0)
            {
                timeEntriesByDateOfUser = timeEntriesByDateOfUser.Where(x => reportsGridData.CurrentQuery.ClientIds.Contains(x.Project.ClientId) || x.Project.ClientId == null && reportsGridData.CurrentQuery.ClientIds.Contains(WithoutClient.Id));
            }

            return timeEntriesByDateOfUser;
        }

        private void CheckAndSetIfInFilterChooseSingleProject(ReportsGridView reportsGridData, IQueryable<TimeEntry> timeEntriesByDateOfUser)
        {
            if (reportsGridData.CurrentQuery.ProjectIds.Length == 1)
            {
                var singleFilteredProjectId = reportsGridData.CurrentQuery.ProjectIds.FirstOrDefault();
                SingleFilteredProjectName = Uow.ProjectRepository.LinkedCacheGetById(singleFilteredProjectId).Name;
            }
        }

        private IQueryable<TimeEntry> GetTimeEntryByDate(Member currentMember, DateTime dateFrom, DateTime dateTo)
        {
            // #0 Get timeEntriesByDate.s
            var timeEntriesByDate = Uow.TimeEntryRepository.GetQueryWithIncludes()
                .Include(x => x.Project).ThenInclude(x => x.Client)
                .Include(x => x.Member.User)
                .Include(x => x.TaskType)
                .Where(t => t.Date.Date >= dateFrom.Date && t.Date.Date <= dateTo.Date);

            #region Constrain for Admin: return all TimeEntries.

            if (currentMember.User.IsAdmin)
            {
                return timeEntriesByDate;
            }

            #endregion

            #region Constrain for Member. return only TimeEntries that manager is assign.

            if (!currentMember.User.IsAdmin && !currentMember.User.IsManager)
            {
                // #1. TimeEntries. Get tEntries for this member.
                timeEntriesByDate = timeEntriesByDate.Where(t => t.MemberId == currentMember.Id);
            }

            #endregion

            #region Constrain for Manager : return #1 TimeEntries that currentMember is assign, #2 TimeEntries for not assign users at Projects (but TEntries was saved), #4 TimeEntries with global projects that not contains in result.

            if (!currentMember.User.IsAdmin && currentMember.User.IsManager)
            {
                var managerRoleId = Uow.ProjectRoleRepository.LinkedCacheGetList().FirstOrDefault(r => r.Name == ProjectRoleManager).Id;

                var managerProjectIds = Uow.MemberProjectRoleRepository.LinkedCacheGetList()
                    .Where(r => r.MemberId == currentMember.Id && r.RoleId == managerRoleId)
                    .Select(x => x.ProjectId)
                    .ToArray();

                // #1. TimeEntries. Get tEntries for this member and tEntries that is current member is Manager!.
                timeEntriesByDate = timeEntriesByDate.Where(t => t.MemberId == currentMember.Id || managerProjectIds.Contains(t.ProjectId));
            }

            return timeEntriesByDate;

            #endregion
        }

        private Client CreateWithOutClientInstance()
        {
            var getAdminUserById = Uow.UserRepository.LinkedCacheGetList().FirstOrDefault(x => x.Id == "038d14e5-27ef-4b07-89b5-39ea8ed0cbf7");

            var withoutClient = new Client
            {
                Id = WithoutClient.Id,
                Name = WithoutClient.Name,
                Creator = getAdminUserById,
                LastEditor = getAdminUserById,
                CreationDate = DateTime.Now,
                CreatorId = getAdminUserById.Id,
                LastEditorUserId = getAdminUserById.Id,
                LastUpdateDate = DateTime.Now,
            };

            return withoutClient;
        }

        #endregion
    }
}