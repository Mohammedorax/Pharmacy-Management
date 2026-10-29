using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PharmacyManagement.Application;
using PharmacyManagement.Infrastructure.SqlServer;

namespace PharmacyManagement.CoreTests
{
    internal static class ActiveIngredientTests
    {
        internal static int Run()
        {
            int checks = 0;
            var repository = new FakeRepository();
            var service = new ActiveIngredientApplicationService(repository);
            OperationActor actor = new OperationActor { UserName = " pharmacist ", UserRole = " Manager " };

            service.CreateIngredientWithEvidence(" باراسيتامول ", " Paracetamol ", ValidEvidence(), actor);
            Check(repository.Definition.ArabicName == "باراسيتامول" &&
                repository.Definition.LatinName == "Paracetamol" &&
                repository.Evidence.SourceTitle == "WHO monograph" &&
                repository.Actor.UserName == "pharmacist" && repository.Actor.UserRole == "Manager",
                "Active ingredient normalization or actor identity changed.", ref checks);

            Guid ingredientId = Guid.NewGuid();
            service.SaveMapping(ingredientId, " باراسيتامول 500 mg ", "1.125", " mg ", true, actor);
            Check(repository.Mapping.ActiveIngredientId == ingredientId &&
                repository.Mapping.MedicineName == "باراسيتامول 500 mg" &&
                repository.Mapping.StrengthValue == 1.125m && repository.Mapping.StrengthUnit == "mg" &&
                repository.Mapping.IsPrimary,
                "Active ingredient mapping precision or normalization changed.", ref checks);

            service.SaveMapping(ingredientId, "باراسيتامول 500 mg", string.Empty, string.Empty, false, actor);
            Check(!repository.Mapping.StrengthValue.HasValue && repository.Mapping.StrengthUnit == string.Empty,
                "Optional strength mapping changed.", ref checks);

            Reject(() => service.CreateIngredientWithEvidence(" ", "", ValidEvidence(), actor),
                "Blank Arabic name was accepted.", ref checks);
            Reject(() => service.AddEvidence(ingredientId, new ActiveIngredientEvidenceInput
            {
                SourceTitle = "مرجع", SourceUrl = "file:///unsafe", RetrievedOn = DateTime.Today
            }, actor), "Non-HTTP evidence URL was accepted.", ref checks);
            Reject(() => service.AddEvidence(ingredientId, new ActiveIngredientEvidenceInput
            {
                SourceTitle = "مرجع", SourceUrl = "https://example.invalid", RetrievedOn = DateTime.Today.AddDays(1)
            }, actor), "Future evidence retrieval date was accepted.", ref checks);
            Reject(() => service.SaveMapping(ingredientId, "دواء", "1.0001", "mg", false, actor),
                "Four-decimal medical strength was accepted.", ref checks);
            Reject(() => service.SaveMapping(ingredientId, "دواء", "1", string.Empty, false, actor),
                "Strength without unit was accepted.", ref checks);
            RejectUnauthorized(() => service.ToggleIngredient(ingredientId,
                new OperationActor { UserName = " ", UserRole = "Manager" }),
                "Blank operation actor was accepted.", ref checks);
            return checks;
        }

        internal static void VerifyQaReadOnly(string server, string database)
        {
            string name = (database ?? string.Empty).ToLowerInvariant();
            if (!(name.EndsWith("_qa") || name.EndsWith("_test") || name.EndsWith("_sandbox")))
                throw new InvalidOperationException("Explicit QA database suffix required before connection.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                IntegratedSecurity = true,
                ConnectTimeout = 5,
                ApplicationName = "Pharmacy.CoreTests.ActiveIngredient.ReadOnly"
            };
            var repository = new SqlActiveIngredientRepository(() => new SqlConnection(builder.ConnectionString));
            IReadOnlyList<ActiveIngredientSummary> ingredients = repository.GetIngredients();
            IReadOnlyList<string> medicines = repository.GetMedicineNames();
            using (var connection = new SqlConnection(builder.ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM dbo.ActiveIngredientsTb;";
                connection.Open();
                int expected = Convert.ToInt32(command.ExecuteScalar());
                if (ingredients.Count != expected)
                    throw new InvalidOperationException("Active ingredient typed read count changed.");
            }
            int evidence = 0;
            int mappings = 0;
            foreach (ActiveIngredientSummary ingredient in ingredients)
            {
                evidence += repository.GetEvidence(ingredient.ActiveIngredientId).Count;
                mappings += repository.GetMappings(ingredient.ActiveIngredientId).Count;
            }
            Console.WriteLine("PASS: typed active ingredient reads. Database=" + database +
                "; Ingredients=" + ingredients.Count + "; Medicines=" + medicines.Count +
                "; Evidence=" + evidence + "; Mappings=" + mappings + "; PersistentWrites=0.");
        }

        private static ActiveIngredientEvidenceInput ValidEvidence()
        {
            return new ActiveIngredientEvidenceInput
            {
                SourceTitle = " WHO monograph ",
                Publisher = " World Health Organization ",
                SourceUrl = "https://example.invalid/monograph",
                RetrievedOn = DateTime.Today
            };
        }

        private static void Check(bool condition, string message, ref int checks)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Reject(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private static void RejectUnauthorized(Action action, string message, ref int checks)
        {
            try { action(); }
            catch (UnauthorizedAccessException) { checks++; return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeRepository : IActiveIngredientRepository
        {
            internal ActiveIngredientDefinition Definition;
            internal ActiveIngredientEvidenceInput Evidence;
            internal ActiveIngredientMappingInput Mapping;
            internal OperationActor Actor;

            public IReadOnlyList<ActiveIngredientSummary> GetIngredients() => new ActiveIngredientSummary[0];
            public IReadOnlyList<string> GetMedicineNames() => new string[0];
            public IReadOnlyList<ActiveIngredientEvidenceRecord> GetEvidence(Guid ingredientId) => new ActiveIngredientEvidenceRecord[0];
            public IReadOnlyList<ActiveIngredientMappingRecord> GetMappings(Guid ingredientId) => new ActiveIngredientMappingRecord[0];
            public Guid CreateWithEvidence(ActiveIngredientDefinition definition, ActiveIngredientEvidenceInput evidence, OperationActor actor)
            {
                Definition = definition; Evidence = evidence; Actor = actor; return Guid.NewGuid();
            }
            public Guid AddEvidence(Guid ingredientId, ActiveIngredientEvidenceInput evidence, OperationActor actor)
            {
                Evidence = evidence; Actor = actor; return Guid.NewGuid();
            }
            public void DeactivateEvidence(Guid evidenceId, OperationActor actor) { Actor = actor; }
            public void ToggleIngredient(Guid ingredientId, OperationActor actor) { Actor = actor; }
            public void SaveMapping(ActiveIngredientMappingInput mapping, OperationActor actor) { Mapping = mapping; Actor = actor; }
        }
    }
}
