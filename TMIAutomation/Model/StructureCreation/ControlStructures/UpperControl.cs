﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    public class UpperControl : IStructure
    {
        private readonly ILogger logger = Log.ForContext<UpperControl>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string upperPTVId;

        public UpperControl(EsapiWorker esapiWorker, string courseId, string upperPlanId, string upperPTVId)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("UpperControl context: {@context}", new List<string> { this.courseId, this.upperPlanId, this.upperPTVId });

                /*
                * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
                */
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                StructureSet upperSS = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId).StructureSet;
                Structure upperPTV = upperSS.Structures.FirstOrDefault(s => s.Id == this.upperPTVId);
                int bottomSlicePTVWithJunction = upperSS.GetStructureSlices(upperPTV).FirstOrDefault();
                int clearBodyFreeOffset = 3;
                int bodyFreeSliceRemove = bottomSlicePTVWithJunction - clearBodyFreeOffset;

                upperSS.CreateHealthyTissue(upperPTV, logger, progress, message);
                logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

                upperSS.CreateBodyFree(upperPTV, 0, bodyFreeSliceRemove, logger, progress, message);
                logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);

                progress.Report(0.25);
                message.Report("Done!");
            });
        }
    }
}