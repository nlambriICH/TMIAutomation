using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Network;
using EvilDICOM.Network.DIMSE.IOD;
using EvilDICOM.Network.SCUOps;
using Serilog;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    public static class DICOMServices
    {
        private static Entity daemon;
        private static Entity local;
        private static DICOMSCU client;
        private static DICOMSCP receiver;
        private static readonly ILogger logger = Log.ForContext(typeof(DICOMServices));

        public static void Init()
        {
            daemon = new Entity(ConfigExport.DaemonAETitle, ConfigExport.DaemonIP, int.Parse(ConfigExport.DaemonPort));
            logger.Information("Daemon entity {@entity}", daemon);

            local = Entity.CreateLocal(ConfigExport.LocalAETitle, int.Parse(ConfigExport.LocalPort));
            logger.Information("DICOM SCU {@entity}", local);
            client = new DICOMSCU(local);
        }

        public static void CreateSCP()
        {
            receiver = new DICOMSCP(local)
            {
                SupportedAbstractSyntaxes = AbstractSyntax.ALL_RADIOTHERAPY_STORAGE
            };

            string storagePath = Path.GetFullPath(ConfigExport.DICOMStorage);

            // Define the action when a DICOM files comes in
            receiver.DIMSEService.CStoreService.CStorePayloadAction = (dcm, asc) =>
            {
                string patientImageStorage = Path.Combine(storagePath, dcm.GetSelector().PatientID.Data);
                Directory.CreateDirectory(patientImageStorage);
                string dcmFileName = $"{dcm.GetSelector().Modality.Data}.{dcm.GetSelector().SOPInstanceUID.Data}.dcm";
                string path = Path.Combine(patientImageStorage, dcmFileName);

                dcm.Write(path);

                logger.Debug("CStorePayloadAction: patientId {patientId}, file name {fileTarget}",
                             dcm.GetSelector().PatientID.Data,
                             Path.GetFileName(path));

                return true;
            };

            receiver.ListenForIncomingAssociations(true);

            logger.Information("DICOM SCP {@entity}", receiver);
        }

        public static bool ExportDCM(StructureSet ssToExport, string patientId)
        {
            CFinder finder = client.GetCFinder(daemon);

            string ssStudyUID = ssToExport.Image.Series.Study.UID;
            logger.Information("Searching for structure set study UID {studyUID}", ssStudyUID);
            var studies = finder.FindStudies(patientId).Where(study => study.StudyInstanceUID == ssStudyUID);
            logger.Debug("FindStudies: patientId {patientId}, {@studiesID}",
                         patientId,
                         studies.Select(study => study.StudyId));

            string ssSeriesUID = ssToExport.Image.Series.UID;
            logger.Information("Searching for structure set series UID {seriesUID}", ssSeriesUID);
            var series = finder.FindSeries(studies).Where(ser => ser.SeriesInstanceUID == ssSeriesUID);
            logger.Debug("FindSeries: patientId {patientId}, {@seriesID}",
                         patientId,
                         series.Select(ser => ser.SeriesInstanceUID));

            CMover mover = client.GetCMover(daemon);

            var cts = series.Where(ser => ser.Modality == "CT" && ser.SeriesInstanceUID == ssSeriesUID)
                            .SelectMany(ser => finder.FindImages(ser));
            int numOfImages = cts.Count();
            logger.Debug("Found {numberOfCT} CT images", numOfImages);
            bool successCT = numOfImages != 0 && CMoveDCMImages(mover, cts);

            var rtstruct = series.Where(ser => ser.Modality == "RTSTRUCT")
                                 .SelectMany(ser => finder.FindImages(ser))
                                 .Where(ser => ser.SOPInstanceUID == ssToExport.UID);
            numOfImages = rtstruct.Count();
            logger.Debug("Found {numberOfRTSTRUCT} RTSTRUCT images", numOfImages);
            bool successRTSTRUCT = numOfImages != 0 && CMoveDCMImages(mover, rtstruct);

            return successCT && successRTSTRUCT;
        }

        private static bool CMoveDCMImages(CMover mover, IEnumerable<CFindImageIOD> imageInstance)
        {
            ushort msgId = 1;
            int totNumberOfCompletedOps = 0;
            int totNumberOfWarningOps = 0;
            int totNumberOfFailedOps = 0;

            foreach (var img in imageInstance)
            {
                var response = mover.SendCMove(img, ConfigExport.LocalAETitle, ref msgId);

                totNumberOfCompletedOps += response.NumberOfCompletedOps;
                totNumberOfWarningOps += response.NumberOfWarningOps;
                totNumberOfFailedOps += response.NumberOfFailedOps;
            }

            logger.Information("Completed:Warning:Failed ops of DICOM export: {completedOps}:{warningOps}:{failedOps}",
                               totNumberOfCompletedOps,
                               totNumberOfWarningOps,
                               totNumberOfFailedOps);

            return totNumberOfFailedOps == 0;
        }
    }
}
