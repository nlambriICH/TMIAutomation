using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly string SCHEDULE_DIR = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Schedule");
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string lowerPlanId;
        private readonly string scheduleCourseId;
        private readonly DateTime treatmentDate;
        private readonly bool isocentersOnArms;

        public Displacements(EsapiWorker esapiWorker,
                             string courseId,
                             string upperPlanId,
                             string lowerPlanId,
                             string scheduleCourseId,
                             DateTime treatmentDate,
                             bool isocentersOnArms)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.lowerPlanId = lowerPlanId;
            this.scheduleCourseId = scheduleCourseId;
            this.treatmentDate = treatmentDate;
            this.isocentersOnArms = isocentersOnArms;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information(
                    "Displacements context: {@context}",
                    new List<string> { this.courseId, this.upperPlanId, this.lowerPlanId, this.scheduleCourseId, this.treatmentDate.ToString("ddMMyyyy"), this.isocentersOnArms.ToString() });
                
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                IEnumerable<PlanSetup> schedulePlans = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.scheduleCourseId).PlanSetups;

                DisplacementsPage page = new DisplacementsPage(upperPlan, lowerPlan, schedulePlans, this.treatmentDate, this.isocentersOnArms);
                page.PopulateTemplate();
                string content = page.TransformText();

                this.SaveDisplacements(scriptContext.Patient.LastName, scriptContext.Patient.FirstName, content);

            }, isWriteable: false);
        }

        private void SaveDisplacements(string patientLastName, string patientFirstName, string content)
        {
            Directory.CreateDirectory(SCHEDULE_DIR);

            string savePath = Path.Combine(SCHEDULE_DIR, $"{patientLastName}_{patientFirstName}_TMLI_{this.treatmentDate:ddMMyyyy}.txt");
            File.WriteAllText(savePath, content);
        }
    }
}