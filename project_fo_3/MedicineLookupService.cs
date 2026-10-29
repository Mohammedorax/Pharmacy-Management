using System;
using System.Data;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace project_fo_3
{
    internal sealed class MedicineLookupResult
    {
        internal string MedicineName { get; private set; }
        internal string CompanyName { get; private set; }
        internal MedicineLookupResult(string medicineName, string companyName)
        {
            MedicineName = medicineName;
            CompanyName = companyName;
        }
    }

    // Compatibility seam for legacy consumers and QA. SQL is owned by Infrastructure.
    internal static class MedicineLookupService
    {
        internal const string SearchQuery = SqlMedicineSearchRepository.SearchQuery;
        internal const string PopularQuery = SqlMedicineSearchRepository.PopularQuery;
        internal static string BuildPopularQuery(bool hasReturns) => SqlMedicineSearchRepository.BuildPopularQuery(hasReturns);
        internal static string EscapeSearchPattern(string value) => SqlMedicineSearchRepository.EscapeSearchPattern(value);
        internal static MedicineLookupResult FindByBarcode(string barcode, OperationActor actor) => Adapt(ApplicationServices.CreateMedicineBarcodeSession(() => Database.ConnectionString, AppPermission.Purchases).Service.FindByBarcode(barcode, actor));
        internal static MedicineLookupResult ReadUniqueBarcodeMatch(IDataReader reader) => Adapt(SqlMedicineSearchRepository.ReadUniqueBarcodeMatch(reader));
        private static MedicineLookupResult Adapt(MedicineMatch value) => value == null ? null : new MedicineLookupResult(value.MedicineName, value.CompanyName);
    }
}

