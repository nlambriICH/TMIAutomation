using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
	public class LowerControl : IStructure
	{
		private readonly ILogger logger = Log.ForContext<LowerControl>();
		private readonly EsapiWorker esapiWorker;
		private readonly string lowerPlanId;
		private readonly string lowerPTVId;

		public LowerControl(EsapiWorker esapiWorker, string lowerPlanId, string lowerPTVId)
		{
			this.esapiWorker = esapiWorker;
			this.lowerPlanId = lowerPlanId;
			this.lowerPTVId = lowerPTVId;
		}

		public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
		{
			return esapiWorker.RunAsync(scriptContext =>
			{
				logger.Information("LowerControl context: {@context}", new List<string> { this.lowerPlanId, this.lowerPTVId });

				/*
                 * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
                 */
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				StructureSet lowerSS = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId).StructureSet;
				Structure lowerPTV = lowerSS.Structures.FirstOrDefault(s => s.Id == this.lowerPTVId);
				int bottomSlicePTVWithJunction = lowerSS.GetStructureSlices(lowerPTV).LastOrDefault();
				int clearBodyFreeOffset = 3;
				int bodyFreeSliceStart = bottomSlicePTVWithJunction + clearBodyFreeOffset;
				int bodyFreeSliceRemove = lowerSS.Image.ZSize - bottomSlicePTVWithJunction - clearBodyFreeOffset;

				lowerSS.CreateHealthyTissue(lowerPTV, logger, progress, message);
				logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

				lowerSS.CreateBodyFree(lowerPTV, bodyFreeSliceStart, bodyFreeSliceRemove, logger, progress, message);
				logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);

				progress.Report(0.25);
				message.Report("Done!");
			});
		}
	}
}
