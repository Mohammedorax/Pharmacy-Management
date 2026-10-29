using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using PharmacyManagement.Application;

namespace PharmacyManagement.Infrastructure.SqlServer
{
    public sealed class SqlInvoiceImportDraftRepository : IInvoiceImportDraftRepository
    {
        private readonly Func<SqlConnection> connectionFactory;

        public SqlInvoiceImportDraftRepository(Func<SqlConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IReadOnlyList<Guid> SaveBatch(IReadOnlyList<InvoiceImportCandidate> candidates, string createdBy)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var ids = new List<Guid>(candidates.Count);
                        foreach (InvoiceImportCandidate candidate in candidates)
                        {
                            string kind = candidate.Kind == ImportedInvoiceKind.Sale ? "Sale" : "Purchase";
                            using (var existing = new SqlCommand(@"SELECT COUNT(*) FROM dbo.ImportedInvoiceSourceTb WITH (UPDLOCK,HOLDLOCK)
WHERE SourceSystem=@System AND InvoiceKind=@Kind AND SourceInvoiceId=@SourceId;", connection, transaction))
                            {
                                BindSource(existing, candidate, kind);
                                if (Convert.ToInt32(existing.ExecuteScalar()) != 0)
                                    throw new InvalidOperationException("هذه الفاتورة معتمدة سابقًا؛ لا يمكن حفظها كمسودة جديدة.");
                            }
                            Guid id = Guid.NewGuid();
                            using (var header = new SqlCommand(@"INSERT dbo.InvoiceImportDraftsTb
(DraftId,SourceSystem,InvoiceKind,SourceInvoiceId,InvoiceDate,PartyName,CurrencyCode,CreatedBy,
PartyReference,PartyPhone,DiscountPercentText,TaxRatePercentText,PriceTaxMode,InvoiceNotes,PaymentMethod)
VALUES(@Id,@System,@Kind,@SourceId,@Date,@Party,@Currency,@Actor,
@PartyReference,@PartyPhone,@Discount,@TaxRate,@TaxMode,@Notes,@Payment);", connection, transaction))
                            {
                                header.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                                BindSource(header, candidate, kind);
                                header.Parameters.Add("@Date", SqlDbType.DateTime2).Value = candidate.InvoiceDate;
                                header.Parameters.Add("@Party", SqlDbType.NVarChar, 200).Value = candidate.PartyName;
                                header.Parameters.Add("@Currency", SqlDbType.Char, 3).Value = candidate.CurrencyCode;
                                header.Parameters.Add("@Actor", SqlDbType.NVarChar, 200).Value = createdBy;
                                Optional(header, "@PartyReference", 50, candidate.PartyReference);
                                Optional(header, "@PartyPhone", 50, candidate.PartyPhone);
                                Optional(header, "@Discount", 40, Invariant(candidate.InvoiceDiscountPercent));
                                Optional(header, "@TaxRate", 40, Invariant(candidate.TaxRatePercent));
                                Optional(header, "@TaxMode", 12, candidate.PriceTaxMode);
                                Optional(header, "@Notes", 1000, candidate.Notes);
                                Optional(header, "@Payment", 20, candidate.PaymentMethod);
                                header.ExecuteNonQuery();
                            }
                            foreach (InvoiceImportRow line in candidate.Lines)
                            {
                                using (var item = new SqlCommand(@"INSERT dbo.InvoiceImportDraftLinesTb
(DraftId,LineNumber,MedicineName,Barcode,QuantityText,UnitPriceText,
ProductionDate,ExpiryDate,BatchNumber,Gtin,SerialNumber)
VALUES(@Id,@Line,@Medicine,@Barcode,@Quantity,@Price,
@Production,@Expiry,@Batch,@Gtin,@Serial);", connection, transaction))
                                {
                                    item.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                                    item.Parameters.Add("@Line", SqlDbType.Int).Value = line.LineNumber;
                                    item.Parameters.Add("@Medicine", SqlDbType.NVarChar, 200).Value = line.MedicineName;
                                    item.Parameters.Add("@Barcode", SqlDbType.NVarChar, 100).Value = line.Barcode;
                                    item.Parameters.Add("@Quantity", SqlDbType.NVarChar, 40).Value = line.Quantity.ToString("G29", CultureInfo.InvariantCulture);
                                    item.Parameters.Add("@Price", SqlDbType.NVarChar, 40).Value = line.UnitPrice.ToString("G29", CultureInfo.InvariantCulture);
                                    item.Parameters.Add("@Production", SqlDbType.Date).Value = line.ProductionDate.HasValue ? (object)line.ProductionDate.Value.Date : DBNull.Value;
                                    item.Parameters.Add("@Expiry", SqlDbType.Date).Value = line.ExpiryDate.HasValue ? (object)line.ExpiryDate.Value.Date : DBNull.Value;
                                    Optional(item, "@Batch", 100, line.BatchNumber);
                                    Optional(item, "@Gtin", 20, line.Gtin);
                                    Optional(item, "@Serial", 100, line.SerialNumber);
                                    item.ExecuteNonQuery();
                                }
                            }
                            ids.Add(id);
                        }
                        transaction.Commit();
                        return ids.AsReadOnly();
                    }
                    catch (SqlException error) when (error.Number == 2627 || error.Number == 2601)
                    {
                        transaction.Rollback();
                        throw new InvalidOperationException("توجد مسودة محفوظة لهذه الفاتورة المصدر؛ راجعها قبل إعادة الاستيراد.", error);
                    }
                    catch (SqlException error) when (error.Number == 208)
                    {
                        transaction.Rollback();
                        throw new InvalidOperationException("مخطط مسودات الفواتير غير مثبت.", error);
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        public IReadOnlyList<InvoiceImportDraftSummary> ListRecent()
        {
            var result = new List<InvoiceImportDraftSummary>();
            using (SqlConnection connection = connectionFactory())
            using (var command = new SqlCommand(@"SELECT TOP (200) DraftId,SourceSystem,InvoiceKind,SourceInvoiceId,
InvoiceDate,PartyName,Status,CreatedAtUtc FROM dbo.InvoiceImportDraftsTb
ORDER BY CreatedAtUtc DESC,DraftId;", connection))
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        result.Add(new InvoiceImportDraftSummary {
                            DraftId = reader.GetGuid(0), SourceSystem = reader.GetString(1),
                            Kind = reader.GetString(2) == "Sale" ? ImportedInvoiceKind.Sale : ImportedInvoiceKind.Purchase,
                            SourceInvoiceId = reader.GetString(3), InvoiceDate = reader.GetDateTime(4),
                            PartyName = reader.GetString(5), Status = reader.GetString(6),
                            CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
                        });
            }
            return result.AsReadOnly();
        }

        public InvoiceImportDraftRecord Get(Guid draftId)
        {
            using (SqlConnection connection = connectionFactory())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string system, sourceId, party, currency, actor, kind, status, reference;
                        string partyReference, partyPhone, discount, taxRate, taxMode, notes, payment;
                        DateTime date, created;
                        using (var header = new SqlCommand(@"SELECT SourceSystem,InvoiceKind,SourceInvoiceId,InvoiceDate,PartyName,
CurrencyCode,CreatedBy,CreatedAtUtc,Status,ApprovedReference,
PartyReference,PartyPhone,DiscountPercentText,TaxRatePercentText,PriceTaxMode,InvoiceNotes,PaymentMethod
FROM dbo.InvoiceImportDraftsTb WHERE DraftId=@Id;", connection, transaction))
                        {
                            header.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = draftId;
                            using (SqlDataReader reader = header.ExecuteReader())
                            {
                                if (!reader.Read()) { transaction.Commit(); return null; }
                                system = reader.GetString(0); kind = reader.GetString(1); sourceId = reader.GetString(2);
                                date = reader.GetDateTime(3); party = reader.GetString(4); currency = reader.GetString(5);
                                actor = reader.GetString(6); created = reader.GetDateTime(7); status = reader.GetString(8);
                                reference = reader.IsDBNull(9) ? null : reader.GetString(9);
                                partyReference = ReadOptional(reader, 10); partyPhone = ReadOptional(reader, 11);
                                discount = ReadOptional(reader, 12); taxRate = ReadOptional(reader, 13);
                                taxMode = ReadOptional(reader, 14); notes = ReadOptional(reader, 15);
                                payment = ReadOptional(reader, 16);
                            }
                        }
                        var rows = new List<InvoiceImportRow>();
                        using (var details = new SqlCommand(@"SELECT LineNumber,MedicineName,Barcode,QuantityText,UnitPriceText,
ProductionDate,ExpiryDate,BatchNumber,Gtin,SerialNumber
FROM dbo.InvoiceImportDraftLinesTb WHERE DraftId=@Id ORDER BY LineNumber;", connection, transaction))
                        {
                            details.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = draftId;
                            using (SqlDataReader reader = details.ExecuteReader())
                                while (reader.Read())
                                    rows.Add(new InvoiceImportRow {
                                        SourceInvoiceId = sourceId, Kind = kind == "Sale" ? ImportedInvoiceKind.Sale : ImportedInvoiceKind.Purchase,
                                        InvoiceDate = date, PartyName = party, CurrencyCode = currency,
                                        LineNumber = reader.GetInt32(0), MedicineName = reader.GetString(1), Barcode = reader.GetString(2),
                                        Quantity = decimal.Parse(reader.GetString(3), NumberStyles.Float, CultureInfo.InvariantCulture),
                                        UnitPrice = decimal.Parse(reader.GetString(4), NumberStyles.Float, CultureInfo.InvariantCulture),
                                        PartyReference = partyReference, PartyPhone = partyPhone,
                                        InvoiceDiscountPercent = ParseOptional(discount), TaxRatePercent = ParseOptional(taxRate),
                                        PriceTaxMode = taxMode, Notes = notes, PaymentMethod = payment,
                                        ProductionDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                        ExpiryDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                        BatchNumber = ReadOptional(reader, 7), Gtin = ReadOptional(reader, 8),
                                        SerialNumber = ReadOptional(reader, 9)
                                    });
                        }
                        InvoiceImportCandidate candidate = InvoiceImportValidation.Validate(InvoiceImportMode.Draft, system, rows).Single();
                        transaction.Commit();
                        return new InvoiceImportDraftRecord { DraftId = draftId, Candidate = candidate, CreatedBy = actor,
                            CreatedAtUtc = DateTime.SpecifyKind(created, DateTimeKind.Utc), Status = status, ApprovedReference = reference };
                    }
                    catch { transaction.Rollback(); throw; }
                }
            }
        }

        private static void BindSource(SqlCommand command, InvoiceImportCandidate candidate, string kind)
        {
            command.Parameters.Add("@System", SqlDbType.NVarChar, 100).Value = candidate.SourceSystem;
            command.Parameters.Add("@Kind", SqlDbType.NVarChar, 8).Value = kind;
            command.Parameters.Add("@SourceId", SqlDbType.NVarChar, 100).Value = candidate.SourceInvoiceId;
        }
        private static string Invariant(decimal? value)
        { return value.HasValue ? value.Value.ToString("G29", CultureInfo.InvariantCulture) : null; }
        private static decimal? ParseOptional(string value)
        { return value.Length == 0 ? (decimal?)null : decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private static string ReadOptional(SqlDataReader reader, int index)
        { return reader.IsDBNull(index) ? string.Empty : reader.GetString(index); }
        private static void Optional(SqlCommand command, string name, int length, string value)
        { command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = string.IsNullOrEmpty(value) ? (object)DBNull.Value : value; }
    }
}
