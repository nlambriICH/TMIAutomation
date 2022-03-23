using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMILegsOptimization
{
    static class PostCalculation
    {
        // 98% covers 98% of the target volume
        public static void Normalize(this ExternalPlanSetup externalPlanSetup)
        {
            Structure ptvTotNoJunction = externalPlanSetup.StructureSet.Structures.FirstOrDefault(s => s.Id == "PTVTotNoJunction");
            
            if (ptvTotNoJunction == null)
            {
                Log.Warning("Could not find structure: PTVTotNoJunction. Skip plan normalization");
                return;
            }

            externalPlanSetup.PlanNormalizationValue = 100.0; // no plan normalization
            double targetDose = externalPlanSetup.TotalDose.Dose;
            double doseNormalizationTarget = (98.0 / 100.0) * targetDose;
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
