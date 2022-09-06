using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.StructureCreation
{
	public class ModelBase
	{
		private readonly EsapiWorker esapiWorker;
		public enum PlanType
		{
			Up,
			Down
		}

		public ModelBase(EsapiWorker esapiWorker)
		{
			this.esapiWorker = esapiWorker;
		}

		public Task<List<string>> GetPlansAsync(PlanType planType)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				return targetCourse.PlanSetups.Where(p => p.Id.IndexOf(planType.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
											  .OrderByDescending(p => p.CreationDateTime)
											  .Select(s => s.Id)
											  .ToList();
			},
			isWriteable: false);
		}

		public Task<List<string>> GetPTVsFromPlanAsync(string planId)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);
				return selectedPlan == null ? new List<string>()
				: selectedPlan.StructureSet.Structures.Where(s => s.DicomType == "PTV")
													  .OrderByDescending(s => s.Volume)
													  .Select(s => s.Id)
													  .ToList();

			},
			isWriteable: false);
		}

		public Task<List<string>> GetPTVsFromSSAsync(List<string> excludeSSOfPlanIds)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				StructureSet targetSS = GetTargetStructureSet(scriptContext, excludeSSOfPlanIds);
				return targetSS.Structures.Where(s => s.DicomType == "PTV")
										  .OrderByDescending(s => s.Volume)
										  .Select(s => s.Id)
										  .ToList();
			},
			isWriteable: false);
		}

		private StructureSet GetTargetStructureSet(ScriptContext scriptContext, List<string> excludeSSOfPlanIds)
		{
			Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
			bool isOpenedSSToExclude = targetCourse.PlanSetups.Where(p => excludeSSOfPlanIds.Contains(p.Id))
															  .Select(p => p.StructureSet)
															  .Contains(scriptContext.StructureSet);
			return scriptContext.StructureSet == null || isOpenedSSToExclude
				? targetCourse.PlanSetups.Select(p => p.StructureSet).OrderBy(c => c.HistoryDateTime).Last()
				: scriptContext.StructureSet;
		}

		public Task<List<string>> GetRegistrationsAsync()
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				return scriptContext.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime)
														  .Select(reg => reg.Id)
														  .ToList();
			},
			isWriteable: false);
		}

		public Task GenerateUpperJunctionAsync(string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
		{
			UpperJunction upperJunction = new UpperJunction(this.esapiWorker, upperPlanId, upperPTVId);
			return upperJunction.CreateAsync(progress, message);
		}

		public Task GenerateUpperControlAsync(string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
		{
			UpperControl upperControl = new UpperControl(this.esapiWorker, upperPlanId, upperPTVId);
			return upperControl.CreateAsync(progress, message);
		}

		public Task GenerateLowerPlanAsync(List<string> excludeSSOfPlanIds)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				StructureSet targetSS = GetTargetStructureSet(scriptContext, excludeSSOfPlanIds);

				ExternalPlanSetup newPlan = targetCourse.AddExternalPlanSetup(targetSS);
				int numOfAutoPlans = targetCourse.PlanSetups.Count(p => p.Id.Contains("TMLIdownAuto"));
				newPlan.Id = numOfAutoPlans == 0 ? "TMLIdownAuto" : string.Concat("TMLIdownAuto", numOfAutoPlans);
			});
		}

		public Task<bool> IsPlanDoseValid(string planId)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				PlanSetup upperPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);

				return upperPlan.IsDoseValid;
			});
		}

		public Task GenerateLowerJunctionAsync(string upperPlanId,
											   string lowerPlanId,
											   string lowerPTVId,
											   string registrationId,
											   IProgress<double> progress,
											   IProgress<string> message)
		{
			LowerJunction lowerJunction = new LowerJunction(this.esapiWorker, upperPlanId, lowerPlanId, lowerPTVId, registrationId);
			return lowerJunction.CreateAsync(progress, message);
		}

		public Task GenerateLowerControlAsync(string lowerPlanId,
											  string lowerPTVId,
											  IProgress<double> progress,
											  IProgress<string> message)
		{
			LowerControl lowerControl = new LowerControl(this.esapiWorker, lowerPlanId, lowerPTVId);
			return lowerControl.CreateAsync(progress, message);
		}

		public Task<string> GetMachineNameAsync(string planId)
		{
			return this.esapiWorker.RunAsync(scriptContext =>
			{
				Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
				PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);
				return selectedPlan == null ? string.Empty
				: selectedPlan.Beams.Select(b => b.TreatmentUnit.Id).FirstOrDefault();
			});
		}

		public Task OptimizeAsync(string lowerPlanId,
								  string machineName,
								  IProgress<double> progress,
								  IProgress<string> message)
		{
			Optimization optimization = new Optimization(this.esapiWorker, lowerPlanId, machineName);
			return optimization.ComputeAsync(progress, message);
		}
	}
}
