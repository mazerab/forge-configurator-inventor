﻿/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using WebApplication.State;
using WebApplication.Definitions;
using WebApplication.Utilities;
using WebApplication.Services;

namespace WebApplication.Job
{
    internal class AdoptProjectWithParametersJobItem : JobItemBase
    {
        private readonly ProjectService _projectService;
        private readonly string _payloadUrl;
        private readonly DtoGenerator _dtoGenerator;
        private readonly AdoptProjectWithParametersPayloadProvider _adoptProjectWithParametersPayloadProvider;

        public AdoptProjectWithParametersJobItem(ILogger logger, ProjectService projectService, string payloadUrl, 
            DtoGenerator dtoGenerator, AdoptProjectWithParametersPayloadProvider adoptProjectWithParametersPayloadProvider)
            : base(logger, null, null)
        {
            _projectService = projectService;
            _payloadUrl = payloadUrl;
            _dtoGenerator = dtoGenerator;
            _adoptProjectWithParametersPayloadProvider = adoptProjectWithParametersPayloadProvider;
        }

        public override async Task ProcessJobAsync(IResultSender resultSender)
        {
            using var scope = Logger.BeginScope("Project Adoption ({Id})");

            try
            {
                AdoptProjectWithParametersPayload payload = await _adoptProjectWithParametersPayloadProvider.GetParametersAsync(_payloadUrl);

                Logger.LogInformation($"ProcessJob (AdoptProjectWithParameters) {Id} for project {payload.Name} started.");

                ProjectStorage projectStorage = await _projectService.AdoptProjectWithParametersAsync(payload);

                Logger.LogInformation($"ProcessJob (AdoptProjectWithParameters) {Id} for project {payload.Name} completed.");
                
                await resultSender.SendSuccessAsync(Tuple.Create(_dtoGenerator.ToDTO(projectStorage), payload));
            }
            catch (Exception ex)
            {
                await resultSender.SendErrorAsync(Id, ex.Message);
            }
        }
    }
}