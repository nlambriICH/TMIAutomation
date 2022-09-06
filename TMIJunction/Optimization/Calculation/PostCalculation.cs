using Serilog;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
	static class PostCalculation
	{
		private static readonly ILogger logger = Log.ForContext(typeof(PostOptimization));

		// 98% covers 98% of the target volume
		public static void Normalize(this ExternalPlanSetup externalPlanSetup)
		{
			Structure ptvTotNoJunction = externalPlanSetup.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_NO_JUNCTION);

			if (ptvTotNoJunction == null)
			{
				logger.Warning("Could not find structure: {ptvTotNoJunction}. Skip plan normalization", StructureHelper.LOWER_PTV_NO_JUNCTION);
				return;
			}

			externalPlanSetup.PlanNormalizationValue = 100.0; // no plan normalization
			double targetDose = externalPlanSetup.TotalDose.Dose;
			double doseNormalizationTarget = 98.0 / 100.0 * targetDose;
			DoseValue doseValue = new DoseValue(doseNormalizationTarget, "Gy");

			double volumeNormalizationTarget = 98.0;
			double volume = externalPlanSetup.GetVolumeAtDose(ptvTotNoJunction, doseValue, VolumePresentation.Relative);

			if (volume < volumeNormalizationTarget)
			{
				doseValue = externalPlanSetup.GetDoseAtVolume(ptvTotNoJunction, volumeNormalizationTarget, VolumePresentation.Relative, DoseValuePresentation.Absolute);
				externalPlanSetup.PlanNormalizationValue = 100.0 * doseValue.Dose / doseNormalizationTarget;
			}
		}
	}
}
