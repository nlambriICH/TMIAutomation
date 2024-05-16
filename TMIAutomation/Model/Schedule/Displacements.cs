using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;
using TMIAutomation.Model.Schedule.Templates;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    public class Displacements
    {
        private readonly ILogger logger = Log.ForContext<Displacements>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string lowerPlanId;
        private readonly string scheduleCourseId;
        private readonly DateTime treatmentDate;
        private static readonly string SCHEDULE_DIR = "Schedule";

        public Displacements(EsapiWorker esapiWorker,
                             string courseId,
                             string upperPlanId,
                             string lowerPlanId,
                             string scheduleCourseId,
                             DateTime treatmentDate)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.lowerPlanId = lowerPlanId;
            this.scheduleCourseId = scheduleCourseId;
            this.treatmentDate = treatmentDate;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("Displacements context: {@context}", new List<string> { this.courseId, this.upperPlanId, this.lowerPlanId, this.scheduleCourseId });
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                IEnumerable<PlanSetup> schedulePlans = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.scheduleCourseId).PlanSetups;

                DisplacementsPage page = new DisplacementsPage(upperPlan, lowerPlan, schedulePlans, this.treatmentDate);
                string content = page.TransformText();

                this.SaveDisplacements(scriptContext.Patient.LastName, scriptContext.Patient.FirstName, content);

            }, isWriteable: false);
        }

        private void SaveDisplacements(string patientLastName, string patientFirstName, string content)
        {
            if (!Directory.Exists(SCHEDULE_DIR))
            {
                Directory.CreateDirectory(SCHEDULE_DIR);
            }

            File.WriteAllText($"{patientLastName}_{patientFirstName}_TMLI_{this.treatmentDate:ddMMyyyy}.txt", content);
        }
    }
}
