﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CsvTextEditorIsDirtyProjectWatcher.cs" company="WildGums">
//   Copyright (c) 2008 - 2017 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace CsvTextEditor.ProjectManagement
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows.Threading;
    using Catel;
    using Catel.Configuration;
    using Catel.IoC;
    using Catel.Threading;
    using Models;
    using Orc.CsvTextEditor;
    using Orc.ProjectManagement;
    using Services;

    public class CsvTextEditorIsDirtyProjectWatcher : ProjectWatcherBase
    {
        #region Fields
        private readonly IMainWindowTitleService _mainWindowTitleService;
        private readonly ISaveProjectChangesService _saveProjectChangesService;
        private readonly IConfigurationService _configurationService;
        private readonly IProjectManager _projectManager;
        private readonly IServiceLocator _serviceLocator;
        private ICsvTextEditorInstance _csvTextEditorInstance;
        private DispatcherTimer _autoSaveTimer;
        #endregion

        #region Constructors
        public CsvTextEditorIsDirtyProjectWatcher(IProjectManager projectManager, IServiceLocator serviceLocator, IMainWindowTitleService mainWindowTitleService,
            ISaveProjectChangesService saveProjectChangesService, IConfigurationService configurationService)
            : base(projectManager)
        {
            Argument.IsNotNull(() => serviceLocator);
            Argument.IsNotNull(() => mainWindowTitleService);
            Argument.IsNotNull(() => saveProjectChangesService);
            Argument.IsNotNull(() => configurationService);
            Argument.IsNotNull(() => projectManager);

            _projectManager = projectManager;
            _serviceLocator = serviceLocator;
            _mainWindowTitleService = mainWindowTitleService;
            _saveProjectChangesService = saveProjectChangesService;
            _configurationService = configurationService;

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Tick += AutoSaveIfNeeded;
            _autoSaveTimer.Interval = configurationService.GetRoamingValue(Configuration.AutoSaveInterval, Configuration.AutoSaveIntervalDefaultValue);
            _autoSaveTimer.Start();
        }
        #endregion

        #region Methods
        protected override Task OnActivatedAsync(IProject oldProject, IProject newProject)
        {
            if (_csvTextEditorInstance != null)
                _csvTextEditorInstance.TextChanged -= CsvTextEditorInstanceOnTextChanged;

            if (newProject == null)
                return TaskHelper.Completed;

            if (_csvTextEditorInstance == null && _serviceLocator.IsTypeRegistered<ICsvTextEditorInstance>(newProject))
                _csvTextEditorInstance = _serviceLocator.ResolveType<ICsvTextEditorInstance>(newProject);

            if (_csvTextEditorInstance != null)
                _csvTextEditorInstance.TextChanged += CsvTextEditorInstanceOnTextChanged;

            return base.OnActivatedAsync(oldProject, newProject);
        }

        private void AutoSaveIfNeeded(object sender, EventArgs e)
        {

            if (_csvTextEditorInstance == null)
            {
                return;
            }
                          
            if (_csvTextEditorInstance.IsDirty &&
                _configurationService.GetRoamingValue<bool>(Configuration.AutoSaveEditor))
            {
                var project = (Project)ProjectManager.ActiveProject;
                _projectManager.SaveAsync(project.Location).ConfigureAwait(false);

            }
        }

        private void CsvTextEditorInstanceOnTextChanged(object sender, EventArgs e)
        {
            var project = (Project) ProjectManager.ActiveProject;

            project?.SetIsDirty(_csvTextEditorInstance.IsDirty);
            _mainWindowTitleService.UpdateTitle();
        }

        protected override Task OnSavedAsync(IProject project)
        {
            var csvProject = (Project) ProjectManager.ActiveProject;
            csvProject?.SetIsDirty(_csvTextEditorInstance.IsDirty);
            _mainWindowTitleService.UpdateTitle();

            return base.OnSavedAsync(project);
        }

        protected override async Task OnClosingAsync(ProjectCancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            if (!await _saveProjectChangesService.EnsureChangesSavedAsync((Project)e.Project, SaveChangesReason.Closing))
            {
                e.Cancel = true;
            }
        }
        #endregion
    }
}