using AllInsurance.Application.Bridges;
using AllInsurance.Application.Exports;
using AllInsurance.Application.Business;
using AllInsurance.Domain.Entities;
using System.Text;

namespace AllInsurance.Tests;

public sealed class BridgeAndExportTests
{
    [Fact]
    public void ExportNamesStayShortAndDistinguishFilters()
    {
        var now = new DateTime(2026, 11, 9, 14, 3, 2);
        var one = ExportFileName.Create("Πίνακας παραγωγής " + new string('α', 900), now);
        var two = ExportFileName.Create("Πίνακας παραγωγής " + new string('β', 900), now);
        Assert.Matches("^KSS-PROD-20261109-140302-[0-9A-F]{6}$", one);
        Assert.True(one.Length <= 40);
        Assert.NotEqual(one, two);
    }

    [Theory]
    [InlineData(NativeBridgeSource.Ydrogeios)]
    [InlineData(NativeBridgeSource.Orizon)]
    public void PositionalFormatParsesWithoutRealCustomerData(NativeBridgeSource source)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var fields = new string[source == NativeBridgeSource.Ydrogeios ? 65 : 66];
        Array.Fill(fields, "");
        fields[0] = "1234567890";
        fields[1] = "0000000000";
        fields[5] = "01092026";
        fields[6] = "02092026";
        fields[7] = "02032027";
        fields[8] = "01";
        fields[9] = source == NativeBridgeSource.Ydrogeios ? "0000*" : "00071676";
        fields[10] = "008";
        fields[12] = "ΔΟΚΙΜΗ ΠΕΛΑΤΗ";
        fields[16] = "30400";
        fields[19] = "123456789";
        fields[27] = "ΑΒΓ1234";
        fields[28] = "320";
        fields[34] = "OPEL";
        fields[52] = "+00000009023";
        fields[59] = "+00000013544";
        var start = source == NativeBridgeSource.Ydrogeios ? 61 : 62;
        fields[start] = "001";
        fields[start + 1] = "+0001200";
        fields[start + 2] = "+0000000";
        fields[start + 3] = "+000000100";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, string.Join(source == NativeBridgeSource.Ydrogeios ? ',' : ';',
                fields), source == NativeBridgeSource.Ydrogeios ? Encoding.GetEncoding(737) :
                new UTF8Encoding(false));
            var row = Assert.Single(NativePolicyBridge.Read(path, source));
            Assert.Equal("1234567890", row.PolicyNumber);
            Assert.Equal("ΔΟΚΙΜΗ ΠΕΛΑΤΗ", row.CustomerName);
            Assert.Equal(90.23m, row.NetPremium);
            Assert.Equal(135.44m, row.GrossPremium);
            Assert.Equal(12m, Assert.Single(row.Coverages).SumInsured);
            Assert.ThrowsAny<Exception>(() => NativePolicyBridge.Read(path,
                source == NativeBridgeSource.Ydrogeios ? NativeBridgeSource.Orizon :
                NativeBridgeSource.Ydrogeios));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SuppliedNativeSamplesParseCompletelyWhenAvailable()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        var yd = Path.Combine(downloads, "PARG101290 (18).csv");
        var or = Path.Combine(downloads,
            "carhist_on_28-09-26_10-02-15_for_Vehicle_71676_28-06-2026_28-09-2026.txt");
        if (!File.Exists(yd) || !File.Exists(or)) return;
        var yRows = NativePolicyBridge.Read(yd, NativeBridgeSource.Ydrogeios);
        var oRows = NativePolicyBridge.Read(or, NativeBridgeSource.Orizon);
        Assert.Equal(102, yRows.Count);
        Assert.Equal(110, oRows.Count);
        Assert.All(yRows.Concat(oRows), row =>
        {
            Assert.Equal(9, row.CustomerTaxNumber.Length);
            Assert.True(row.StartDate <= row.EndDate);
            Assert.NotEmpty(row.PolicyNumber);
        });
    }

    [Fact]
    public async Task SuppliedNativeSamplesCanBeSavedToIsolatedDatabaseWhenAvailable()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        var files = new[]
        {
            (Path.Combine(downloads, "PARG101290 (18).csv"), NativeBridgeSource.Ydrogeios,
                "Υδρόγειος"),
            (Path.Combine(downloads,
                "carhist_on_28-09-26_10-02-15_for_Vehicle_71676_28-06-2026_28-09-2026.txt"),
                NativeBridgeSource.Orizon, "Ορίζων 1964")
        };
        if (files.Any(item => !File.Exists(item.Item1))) return;
        await using var environment = new TestEnvironment();
        await environment.CreateAdministratorAsync();
        await environment.AuthenticationService.SignInAsync("first.admin", "A strong passphrase 2026!");
        var records = new BusinessRecordService(environment.ContextFactory, environment.Session);
        var all = files.Select(item => (item.Item3,
            Rows: NativePolicyBridge.Read(item.Item1, item.Item2))).ToArray();
        var customerIds = all.SelectMany(item => item.Rows).Select(item => item.CustomerTaxNumber)
            .Distinct().ToDictionary(tax => tax, _ => Guid.NewGuid());
        await using (var db = await environment.ContextFactory.CreateDbContextAsync())
        {
            db.Customers.AddRange(customerIds.Select(item => new Customer
            {
                Id = item.Value, LastName = "Δοκιμή", FirstName = "Γέφυρα",
                TaxNumber = item.Key
            }));
            await db.SaveChangesAsync();
        }
        var imported = 0;
        var cancellations = 0;
        var duplicates = 0;
        foreach (var (company, rows) in all)
        {
            var companyId = await records.SaveAsync(new BusinessRecord
                { SectionKey = "companies", Title = company });
            var byNumber = new Dictionary<string, BusinessRecord>(StringComparer.OrdinalIgnoreCase);
            var cancelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vehicles = new Dictionary<(Guid CustomerId, string Registration), Guid>();
            var coverageNames = rows.SelectMany(row => row.Coverages)
                .Select(item => item.Code).Distinct().ToDictionary(code => code,
                    code => "Κάλυψη " + code);
            foreach (var row in rows)
            {
                if (row.IsCancellation)
                {
                    if (!cancelled.Add(row.PolicyNumber)) { duplicates++; continue; }
                    if (byNumber.TryGetValue(row.PolicyNumber, out var current))
                    {
                        var old = BusinessRecordDetails.Read<PolicyCardData>(current.DetailsJson);
                        old.CancellationDate = row.IssueDate;
                        old.CancellationReason = "Ακύρωση από γέφυρα " + company;
                        current.Status = "Ακυρωμένο";
                        current.DetailsJson = BusinessRecordDetails.Write(old);
                        await records.SaveAsync(current);
                        cancellations++;
                        continue;
                    }
                }
                else if (byNumber.ContainsKey(row.PolicyNumber)) { duplicates++; continue; }
                Guid? vehicleId = null;
                if (row.Registration is not null)
                {
                    var key = (customerIds[row.CustomerTaxNumber], row.Registration);
                    if (!vehicles.TryGetValue(key, out var savedVehicle))
                    {
                        savedVehicle = await records.SaveAsync(new BusinessRecord
                        {
                            SectionKey = "vehicles", Title = row.Registration,
                            Reference = row.Registration, CustomerId = key.Item1,
                            Status = "Ενεργό",
                            DetailsJson = BusinessRecordDetails.Write(new VehicleCardData
                            {
                                Registration = row.Registration, Make = row.Make,
                                Use = "Χρήση " + row.UseCode,
                                People = [new VehiclePerson { CustomerId = key.Item1,
                                    Role = "Ιδιοκτήτης", OwnershipPercent = 100,
                                    IsPrimaryOwner = true }]
                            })
                        });
                        vehicles[key] = savedVehicle;
                    }
                    vehicleId = savedVehicle;
                }
                var data = NativePolicyBridge.ToPolicyData(row, companyId, company,
                    "Κλάδος " + row.BranchCode, "Χρήση " + row.UseCode,
                    coverageNames, null, vehicleId, row.PreviousPolicyNumber is not null,
                    null);
                var record = new BusinessRecord
                {
                    SectionKey = "policies", Title = row.PolicyNumber,
                    Reference = row.PolicyNumber,
                    CustomerId = customerIds[row.CustomerTaxNumber],
                    DocumentType = "Ασφαλιστήριο",
                    Status = row.IsCancellation ? "Ακυρωμένο" : "Ενεργό",
                    EventDate = row.StartDate,
                    DetailsJson = BusinessRecordDetails.Write(data)
                };
                record.Id = await records.SaveAsync(record);
                byNumber[row.PolicyNumber] = record;
                imported++;
            }
        }
        Assert.Equal(212, imported + cancellations + duplicates);
        Assert.True(imported >= 200);
        Assert.True(cancellations >= 8);
    }
}
