using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    class LegsJunction : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string legsPlanId;
        private readonly string legsPTVId;
        private readonly string imageRegId;

        public LegsJunction(string bodyPlanId, string legsPlanId, string legsPTVId, string imageRegId)
        {
            this.bodyPlanId = bodyPlanId;
            this.legsPlanId = legsPlanId;
            this.legsPTVId = legsPTVId;
            this.imageRegId = imageRegId;
        }

        public void Create(ScriptContext context)
		{
			PlanSetup bodyPlan = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId);
			StructureSet bodySS = bodyPlan.StructureSet;
			StructureSet legsSS = context.PlansInScope.FirstOrDefault(p => p.Id == legsPlanId).StructureSet;

			List<string> isoStructuresId = new List<string> { StructureHelper.DOSE_25, StructureHelper.DOSE_50, StructureHelper.DOSE_75, StructureHelper.DOSE_100 };

			if (isoStructuresId.All(id => legsSS.Structures.Select(s => s.Id).Contains(id)))
			{
				CreateJunctionSubstructures(legsSS);
			}
			else
			{
				if (bodySS.Image.ZRes != legsSS.Image.ZRes)
				{
					MessageBoxResult msgBoxResult = ShowWarning(bodySS.Image.ZRes, legsSS.Image.ZRes);

					if (msgBoxResult == MessageBoxResult.No || msgBoxResult == MessageBoxResult.None)
					{
						return;
					}

				}

				context.Patient.BeginModifications();

				/*
				 * Isodose levels body CT
				 */
				List<double> doseValues = null;
				DoseValue.DoseUnit doseUnit = DoseValue.DoseUnit.Unknown;

				if (bodyPlan.DoseValuePresentation == DoseValuePresentation.Absolute)
				{
					double dosePerFraction = bodyPlan.DosePerFraction.Dose;
					doseValues = new List<double> { 0.25, 0.50, 0.75, 1.0 };
					doseValues.ForEach(d => Math.Round(d * dosePerFraction, 2, MidpointRounding.AwayFromZero));
					doseUnit = DoseValue.DoseUnit.Gy;
				}
				else
				{
					doseValues = new List<double> { 25.0, 50.0, 75.0, 100.0 };
					doseUnit = DoseValue.DoseUnit.Percent;
				}

				List<Structure> bodyIsodoseStructures = new List<Structure>
				{
					bodySS.AddStructure("CONTROL", StructureHelper.DOSE_25),
					bodySS.AddStructure("CONTROL", StructureHelper.DOSE_50),
					bodySS.AddStructure("CONTROL", StructureHelper.DOSE_75),
					bodySS.AddStructure("CONTROL", StructureHelper.DOSE_100)
				};

				for (int i = 0; i < bodyIsodoseStructures.Count; ++i)
				{
					bodyIsodoseStructures[i].ConvertDoseLevelToStructure(bodyPlan.Dose, new DoseValue(doseValues[i], doseUnit));
				}

				/*
				 * Copy structures to legs RTSTRUCT
				 */
				Registration registration = context.Patient.Registrations.FirstOrDefault(reg => reg.Id == imageRegId);

				bodyIsodoseStructures.ForEach(isoStructure =>
				{
					Structure legIsoDose = legsSS.AddStructure(isoStructure.DicomType, isoStructure.Id);
					foreach (int slice in StructureHelper.GetStructureSlices(isoStructure, bodySS))
					{
						VVector[][] contours = isoStructure.GetContoursOnImagePlane(slice);
						foreach (VVector[] contour in contours)
						{
							IEnumerable<VVector> transformedContour = contour.Select(vv => bodySS.Image.FOR == registration.SourceFOR ? registration.TransformPoint(vv) : registration.InverseTransformPoint(vv));
							double z = transformedContour.FirstOrDefault().z;

							legIsoDose.AddContourOnImagePlane(transformedContour.ToArray(), StructureHelper.GetSlice(z, legsSS));
						}
					}
				});

				if (bodySS.Image.ZRes != legsSS.Image.ZRes)
				{
					List<Structure> legsIsoDoseStructures = new List<Structure>
					{
						legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25),
						legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_50),
						legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_75),
						legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100)
					};

					foreach (Structure legIsoDose in legsIsoDoseStructures)
					{
						legIsoDose.SegmentVolume = legIsoDose.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 0, 0, 0, 0, legsSS.Image.ZRes));
					}
				}

				CreateJunctionSubstructures(legsSS);
				CreateREMStructure(legsSS);
			}
		}

		private void CreateREMStructure(StructureSet legsSS)
		{
			/*
			 * Create "REM" structure
			 */
			Structure junction50 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_JUNCTION50);

			int topSliceJunction50 = StructureHelper.GetStructureSlices(junction50, legsSS).FirstOrDefault();

			Structure ptv = legsSS.Structures.FirstOrDefault(s => s.Id == legsPTVId);
			int bottomSlicePTVWithJunction = StructureHelper.GetStructureSlices(ptv, legsSS).LastOrDefault();

			Structure rem = legsSS.AddStructure("AVOIDANCE", StructureHelper.REM);
			Structure isodose25 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);

			foreach (int slice in Enumerable.Range(topSliceJunction50, bottomSlicePTVWithJunction - topSliceJunction50 + 3))
			{
				foreach (VVector[] contour in isodose25.GetContoursOnImagePlane(slice))
				{
					rem.AddContourOnImagePlane(contour, slice);
				}
			}

			Structure legsJunction = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_JUNCTION);
			rem.SegmentVolume = rem.Sub(legsJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));
		}

		private static MessageBoxResult ShowWarning(double bodyZRes, double legsZRes)
		{
			return MessageBox.Show($"Image Z-resolution between body and legs CTs does not match: {bodyZRes} mm vs {legsZRes} mm." +
									"\n\nThe script-generated isodose structures will not match those created from within Eclipse. This will also affect the legs junction." +
									"\n\nContinue anyway?" +
									"\n\n[In order to generate a more accurate legs junction, please create manually the isodose structures with names Dose_25%, Dose_50%, Dose_75%, Dose_100%]", "Warning", MessageBoxButton.YesNo);
		}

		private void CreateJunctionSubstructures(StructureSet legsSS)
		{
			/*
			* Create junction substructures for legs starting from isodoses
			*/
			Structure ptv = legsSS.Structures.FirstOrDefault(s => s.Id == legsPTVId);
			Structure ptvLegsWithJunction = legsSS.AddStructure(ptv.DicomType, StructureHelper.PTV_TOTAL);
			ptvLegsWithJunction.SegmentVolume = ptv.SegmentVolume;
			IEnumerable<int> juncSlices = StructureHelper.GetStructureSlices(ptv, legsSS);

			Structure isodose100 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);
			IEnumerable<int> isoSlices = StructureHelper.GetStructureSlices(isodose100, legsSS);

			// clear contours of ptv on slices where isodose100% is present
			foreach (int slice in juncSlices.Intersect(isoSlices))
			{
				ptvLegsWithJunction.ClearAllContoursOnImagePlane(slice);
			}

			Structure isodose75 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_75);
			Structure junction25 = StructureHelper.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION25, legsSS, ptvLegsWithJunction, isodose75);

			Structure isodose50 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_50);
			Structure junction50 = StructureHelper.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION50, legsSS, ptvLegsWithJunction, isodose50);

			Structure isodose25 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);
			Structure junction75 = StructureHelper.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION75, legsSS, ptvLegsWithJunction, isodose25);

			junction75.SegmentVolume = junction75.Sub(junction50.SegmentVolume);
			junction50.SegmentVolume = junction50.Sub(junction25.SegmentVolume);

			Structure junction100 = legsSS.AddStructure("PTV", StructureHelper.PTV_JUNCTION100);
			// Z-axis points towards the gantry: the first slice is the uppermost when patient is in FFS
			int topSliceJunction75 = StructureHelper.GetStructureSlices(junction75, legsSS).FirstOrDefault();
			for (int i = 1; i <= 2; ++i)
			{
				foreach (VVector[] contour in ptvLegsWithJunction.GetContoursOnImagePlane(topSliceJunction75 - i))
				{
					junction100.AddContourOnImagePlane(contour, topSliceJunction75 - i);
				}
			}

			Structure legsJunction = legsSS.AddStructure("PTV", StructureHelper.PTV_JUNCTION);
			legsJunction.SegmentVolume = junction25.Or(junction50).Or(junction75).Or(junction100);

			Structure ptvTotNoJunctionLegs = legsSS.AddStructure("PTV", StructureHelper.PTV_TOT_NO_JUNCTION);
			ptvTotNoJunctionLegs.SegmentVolume = ptvLegsWithJunction.Sub(legsJunction);
		}
	}
}
