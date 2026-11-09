# Πλήρης κατάλογος ιδιοτήτων μοντέλου

Ο κατάλογος παρακάτω προέρχεται από τις δημόσιες ιδιότητες των οντοτήτων του πηγαίου κώδικα. **Δεν σημαίνει ότι κάθε ιδιότητα εμφανίζεται ως πεδίο στην τρέχουσα καρτέλα.** Για την ορατή διάταξη, τις επιλογές και τις ενέργειες χρησιμοποιήστε τα αρχεία 01–10. Κατά μεταφορά υφιστάμενης βάσης, κρατήστε και παλαιά πεδία ακόμη κι αν δεν έχουν πλέον χειριστήριο.

## `Customer` - `Customer.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `CustomCode` | `string?` | Stored property |
| `LastName` | `string` | Stored property |
| `FirstName` | `string` | Stored property |
| `CompanyName` | `string?` | Stored property |
| `CustomerKind` | `string` | Stored property |
| `PrimaryAddress` | `string?` | Stored property |
| `City` | `string?` | Stored property |
| `Region` | `string?` | Stored property |
| `PostalCode` | `string?` | Stored property |
| `PrimaryMobile` | `string?` | Stored property |
| `PrimaryEmail` | `string?` | Stored property |
| `Occupation` | `string?` | Stored property |
| `TaxNumber` | `string?` | Stored property |
| `TaxOffice` | `string?` | Stored property |
| `BirthDate` | `DateTime?` | Stored property |
| `BirthPlace` | `string?` | Stored property |
| `Nationality` | `string?` | Stored property |
| `Gender` | `string?` | Stored property |
| `SocialSecurityNumber` | `string?` | Stored property |
| `FatherName` | `string?` | Stored property |
| `MotherName` | `string?` | Stored property |
| `MaritalStatus` | `string?` | Stored property |
| `SpouseName` | `string?` | Stored property |
| `HomePhone` | `string?` | Stored property |
| `SecondMobile` | `string?` | Stored property |
| `ThirdMobile` | `string?` | Stored property |
| `InsuranceRegistry` | `string?` | Stored property |
| `LegalForm` | `string?` | Stored property |
| `BusinessActivityCode` | `string?` | Stored property |
| `Employer` | `string?` | Stored property |
| `Source` | `string?` | Stored property |
| `Tags` | `string?` | Stored property |
| `PhotoUrl` | `string?` | Stored property |
| `PassportNumber` | `string?` | Stored property |
| `Zone` | `string?` | Stored property |
| `HasDrivingLicense` | `bool` | Stored property |
| `IsNewDriver` | `bool` | Stored property |
| `NameDay` | `DateTime?` | Stored property |
| `HasPersonalInsurance` | `bool` | Stored property |
| `GroupName` | `string?` | Stored property |
| `OpeningBalance` | `decimal` | Stored property |
| `IsActive` | `bool` | Stored property |
| `IsReferral` | `bool` | Stored property |
| `ReferredByCustomerId` | `Guid?` | Foreign key or linked ID |
| `ReferredByPartnerName` | `string?` | Stored property |
| `ReferredByCustomer` | `Customer?` | Stored property |
| `PartnerName` | `string?` | Stored property |
| `BelongsToOffice` | `bool` | Stored property |
| `IsDriver` | `bool` | Stored property |
| `LicenseStartDate` | `DateTime?` | Stored property |
| `LicenseEndDate` | `DateTime?` | Stored property |
| `LicenseCategory` | `string?` | Stored property |
| `LicenseNumber` | `string?` | Stored property |
| `HLDiploma` | `string?` | Stored property |
| `HasDiscountDocument` | `bool` | Stored property |
| `DiscountDocumentDescription` | `string?` | Stored property |
| `IdentityType` | `string?` | Stored property |
| `IdentityNumber` | `string?` | Stored property |
| `SecondPhone` | `string?` | Stored property |
| `ThirdPhone` | `string?` | Stored property |
| `SecondEmail` | `string?` | Stored property |
| `ThirdEmail` | `string?` | Stored property |
| `BusinessEmail` | `string?` | Stored property |
| `BusinessPhone` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |
| `CreatedAtUtc` | `DateTime` | Audit metadata |
| `UpdatedAtUtc` | `DateTime` | Audit metadata |
| `CreatedByUserId` | `Guid?` | Audit metadata |

## `CustomerDetail` - `CustomerDetail.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `CustomerId` | `Guid` | Foreign key or linked ID |
| `Customer` | `Customer` | Stored property |
| `Category` | `string` | Stored property |
| `Title` | `string` | Stored property |
| `Value` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |
| `EffectiveFrom` | `DateTime?` | Stored property |
| `EffectiveUntil` | `DateTime?` | Stored property |
| `IsHistorical` | `bool` | Stored property |
| `CreatedAtUtc` | `DateTime` | Audit metadata |

## `CustomerActivity` - `CustomerActivity.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `CustomerId` | `Guid` | Foreign key or linked ID |
| `Customer` | `Customer` | Stored property |
| `Action` | `string` | Stored property |
| `Description` | `string` | Stored property |
| `ActorName` | `string` | Stored property |
| `OccurredAtUtc` | `DateTime` | Audit metadata |

## `CustomerTransaction` - `CustomerTransaction.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `CustomerId` | `Guid` | Foreign key or linked ID |
| `Customer` | `Customer` | Stored property |
| `Type` | `string` | Stored property |
| `Amount` | `decimal` | Stored property |
| `PaymentMethod` | `string` | Stored property |
| `Currency` | `string` | Stored property |
| `Reference` | `string?` | Stored property |
| `BusinessRecordId` | `Guid?` | Foreign key or linked ID |
| `BusinessRecord` | `BusinessRecord?` | Stored property |
| `Description` | `string?` | Stored property |
| `OccurredAtUtc` | `DateTime` | Audit metadata |
| `CreatedByUserId` | `Guid?` | Audit metadata |

## `BusinessRecord` - `BusinessRecord.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `SectionKey` | `string` | Stored property |
| `Title` | `string` | Stored property |
| `Reference` | `string?` | Stored property |
| `RelatedParty` | `string?` | Stored property |
| `Status` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |
| `DocumentType` | `string?` | Stored property |
| `DetailsJson` | `string?` | Stored property |
| `EventDate` | `DateTime?` | Stored property |
| `CustomerId` | `Guid?` | Foreign key or linked ID |
| `Customer` | `Customer?` | Stored property |
| `CreatedAtUtc` | `DateTime` | Audit metadata |
| `UpdatedAtUtc` | `DateTime` | Audit metadata |
| `CreatedByUserId` | `Guid?` | Audit metadata |

## `BusinessRecordAttachment` - `BusinessRecordAttachment.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `RecordId` | `Guid` | Foreign key or linked ID |
| `Record` | `BusinessRecord` | Stored property |
| `FileName` | `string` | Stored property |
| `RelativePath` | `string` | Stored property |
| `UploadedAtUtc` | `DateTime` | Stored property |
| `UploadedBy` | `string` | Stored property |

## `PartnerProfileData` - `PartnerProfileData.cs`

| Property | C# type | Migration role |
|---|---|---|
| `FirstName` | `string?` | Stored property |
| `LastName` | `string?` | Stored property |
| `CompanyName` | `string?` | Stored property |
| `TaxNumber` | `string?` | Stored property |
| `TaxOffice` | `string?` | Stored property |
| `PersonType` | `string?` | Stored property |
| `BusinessType` | `string?` | Stored property |
| `ProfessionalCapacity` | `string?` | Stored property |
| `IdentityNumber` | `string?` | Stored property |
| `GemiNumber` | `string?` | Stored property |
| `CooperationManager` | `string?` | Stored property |
| `IsPotential` | `bool` | Stored property |
| `IsSuspended` | `bool` | Stored property |
| `HasEnded` | `bool` | Stored property |
| `HasActiveContract` | `bool` | Stored property |
| `Category` | `string?` | Stored property |
| `Email` | `string?` | Stored property |
| `Phone` | `string?` | Stored property |
| `Address` | `string?` | Stored property |
| `City` | `string?` | Stored property |
| `PostalCode` | `string?` | Stored property |
| `FatherName` | `string?` | Stored property |
| `MotherName` | `string?` | Stored property |
| `Gender` | `string?` | Stored property |
| `BirthDate` | `DateTime?` | Stored property |
| `NameDay` | `DateTime?` | Stored property |
| `BirthPlace` | `string?` | Stored property |
| `Nationality` | `string?` | Stored property |
| `PassportNumber` | `string?` | Stored property |
| `Education` | `string?` | Stored property |
| `RegistryNumber` | `string?` | Stored property |
| `RegistryExpiry` | `DateTime?` | Stored property |
| `Chamber` | `string?` | Stored property |
| `SecondaryEmail` | `string?` | Stored property |
| `ThirdEmail` | `string?` | Stored property |
| `SecondaryPhone` | `string?` | Stored property |
| `Mobile` | `string?` | Stored property |
| `SecondaryMobile` | `string?` | Stored property |
| `ThirdMobile` | `string?` | Stored property |
| `Website` | `string?` | Stored property |
| `WorkAddress` | `string?` | Stored property |
| `WorkCity` | `string?` | Stored property |
| `WorkPostalCode` | `string?` | Stored property |
| `WorkPhone` | `string?` | Stored property |
| `WorkEmail` | `string?` | Stored property |
| `ContractNumber` | `string?` | Stored property |
| `StartDate` | `DateTime?` | Stored property |
| `EndDate` | `DateTime?` | Stored property |
| `CreditLimit` | `decimal?` | Stored property |
| `DefaultCommissionPercent` | `decimal?` | Stored property |
| `BankName` | `string?` | Stored property |
| `Iban` | `string?` | Stored property |
| `BankAccounts` | `List<PartnerBankAccount>` | Stored property |
| `PaymentMethod` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |

## `PartnerBankAccount` - `PartnerProfileData.cs`

| Property | C# type | Migration role |
|---|---|---|
| `BankName` | `string` | Stored property |
| `Iban` | `string` | Stored property |
| `Bic` | `string?` | Stored property |

## `PartnerPayout` - `PartnerPayout.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `PartnerId` | `Guid` | Foreign key or linked ID |
| `PolicyId` | `Guid?` | Foreign key or linked ID |
| `Partner` | `BusinessRecord` | Stored property |
| `Amount` | `decimal` | Stored property |
| `Kind` | `string` | Stored property |
| `Currency` | `string` | Stored property |
| `PaymentMethod` | `string` | Stored property |
| `Reference` | `string?` | Stored property |
| `Description` | `string?` | Stored property |
| `OccurredAtUtc` | `DateTime` | Audit metadata |
| `CreatedByUserId` | `Guid?` | Audit metadata |

## `PolicyCardData` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `CardCode` | `string?` | Stored property |
| `OfferNumber` | `string?` | Stored property |
| `ApplicationNumber` | `string?` | Stored property |
| `NoticeNumber` | `string?` | Stored property |
| `PolicyNumber` | `string?` | Stored property |
| `RenewalNumber` | `string?` | Stored property |
| `PreviousPolicyId` | `Guid?` | Foreign key or linked ID |
| `PreviousPolicyNumber` | `string?` | Stored property |
| `RenewalDate` | `DateTime?` | Stored property |
| `CompanyRenewalDate` | `DateTime?` | Stored property |
| `Insurer` | `string?` | Stored property |
| `CompanyId` | `Guid?` | Foreign key or linked ID |
| `Agency` | `string?` | Stored property |
| `AgencyId` | `Guid?` | Foreign key or linked ID |
| `Branch` | `string?` | Stored property |
| `Product` | `string?` | Stored property |
| `VehicleId` | `Guid?` | Foreign key or linked ID |
| `HolderCustomerId` | `Guid?` | Foreign key or linked ID |
| `IssueDate` | `DateTime?` | Stored property |
| `StartDate` | `DateTime?` | Stored property |
| `EndDate` | `DateTime?` | Stored property |
| `CoverageDurationMonths` | `int?` | Stored property |
| `CoverageDurationValue` | `int?` | Stored property |
| `CoverageDurationUnit` | `string?` | Stored property |
| `PreferredPaymentMethod` | `string?` | Stored property |
| `PaymentRecipient` | `string?` | Stored property |
| `PaymentCode` | `string?` | Stored property |
| `CompanyPaymentDueDate` | `DateTime?` | Stored property |
| `CustomerReceivedAt` | `DateTime?` | Stored property |
| `CancellationDate` | `DateTime?` | Stored property |
| `CancellationReason` | `string?` | Stored property |
| `InsuredValue` | `decimal?` | Stored property |
| `GrossPremium` | `decimal?` | Stored property |
| `NetPremium` | `decimal?` | Stored property |
| `DiscountPercent` | `decimal?` | Stored property |
| `DiscountAmount` | `decimal?` | Stored property |
| `DiscountReason` | `string?` | Stored property |
| `PayInInstallments` | `bool` | Stored property |
| `PreviousInsurer` | `string?` | Stored property |
| `RenewalNotes` | `string?` | Stored property |
| `CoverageNotes` | `string?` | Stored property |
| `PartnerId` | `Guid?` | Foreign key or linked ID |
| `PartnerCommissionPercent` | `decimal?` | Stored property |
| `OfficeCommissionPercent` | `decimal?` | Stored property |
| `CommissionRuleId` | `Guid?` | Foreign key or linked ID |
| `Coverages` | `List<PolicyCoverage>` | Stored property |
| `InsuredObjects` | `List<PolicyInsuredObject>` | Stored property |
| `Endorsements` | `List<PolicyEndorsement>` | Stored property |
| `GreenCards` | `List<PolicyGreenCard>` | Stored property |
| `Installments` | `List<PolicyInstallment>` | Stored property |

## `PolicyInsuredObject` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Kind` | `string` | Stored property |
| `Description` | `string` | Stored property |
| `InsuredValue` | `decimal?` | Stored property |

## `PolicyCoverage` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Stored property |
| `SumInsured` | `decimal?` | Stored property |
| `Deductible` | `decimal?` | Stored property |
| `Premium` | `decimal?` | Stored property |
| `Notes` | `string?` | Stored property |

## `PolicyEndorsement` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Number` | `string` | Stored property |
| `Type` | `string` | Stored property |
| `EffectiveDate` | `DateTime?` | Stored property |
| `PremiumDifference` | `decimal?` | Stored property |
| `Notes` | `string?` | Stored property |

## `PolicyGreenCard` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Number` | `string` | Stored property |
| `StartDate` | `DateTime?` | Stored property |
| `EndDate` | `DateTime?` | Stored property |
| `Countries` | `string?` | Stored property |

## `PolicyInstallment` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Number` | `int` | Stored property |
| `DueDate` | `DateTime?` | Stored property |
| `Amount` | `decimal` | Stored property |
| `Notes` | `string?` | Stored property |

## `VehicleCardData` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `CardCode` | `string?` | Stored property |
| `Registration` | `string?` | Stored property |
| `Make` | `string?` | Stored property |
| `Model` | `string?` | Stored property |
| `Vin` | `string?` | Stored property |
| `ManufactureYear` | `int?` | Stored property |
| `FirstRegistrationDate` | `DateTime?` | Stored property |
| `Use` | `string?` | Stored property |
| `Fuel` | `string?` | Stored property |
| `DisplacementCc` | `int?` | Stored property |
| `PowerKw` | `decimal?` | Stored property |
| `AcquisitionDate` | `DateTime?` | Stored property |
| `Color` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |
| `TaxClass` | `string?` | Stored property |
| `HorsePower` | `decimal?` | Stored property |
| `TransferDate` | `DateTime?` | Stored property |
| `Seats` | `int?` | Stored property |
| `FuelColor` | `string?` | Stored property |
| `RegistrationPermit` | `string?` | Stored property |
| `SpecialLicence` | `string?` | Stored property |
| `DiscountDocuments` | `string?` | Stored property |
| `RoadTax` | `decimal?` | Stored property |
| `ReportForms` | `string?` | Stored property |
| `IsConvertible` | `bool` | Stored property |
| `Doors` | `int?` | Stored property |
| `HasSunroof` | `bool` | Stored property |
| `OverThreeAndHalfTons` | `bool` | Stored property |
| `HasRearDualWheels` | `bool` | Stored property |
| `Tonnage` | `decimal?` | Stored property |
| `WheelbaseCm` | `decimal?` | Stored property |
| `HeightCm` | `decimal?` | Stored property |
| `TrackWidthCm` | `decimal?` | Stored property |
| `HasAlarm` | `bool` | Stored property |
| `HasImmobilizer` | `bool` | Stored property |
| `EstimatedMarketValue` | `decimal?` | Stored property |
| `IsGuarded` | `bool` | Stored property |
| `HasEmissionsCard` | `bool` | Stored property |
| `Co2GramsPerKm` | `decimal?` | Stored property |
| `KteoImagePath` | `string?` | Stored property |
| `KteoStatus` | `string?` | Stored property |
| `NextKteoDate` | `DateTime?` | Stored property |
| `People` | `List<VehiclePerson>` | Stored property |

## `VehiclePerson` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `CustomerId` | `Guid` | Foreign key or linked ID |
| `Role` | `string` | Stored property |
| `OwnershipPercent` | `decimal` | Stored property |
| `IsPrimaryOwner` | `bool` | Stored property |

## `ClaimCardData` - `BusinessRecordDetails.cs`

| Property | C# type | Migration role |
|---|---|---|
| `VehicleId` | `Guid?` | Foreign key or linked ID |

## `WorkspaceCardData` - `WorkspaceCardData.cs`

| Property | C# type | Migration role |
|---|---|---|
| `CompanyId` | `Guid?` | Foreign key or linked ID |
| `AgencyId` | `Guid?` | Foreign key or linked ID |
| `PolicyId` | `Guid?` | Foreign key or linked ID |
| `VehicleId` | `Guid?` | Foreign key or linked ID |
| `PartnerId` | `Guid?` | Foreign key or linked ID |
| `ClaimId` | `Guid?` | Foreign key or linked ID |
| `Fields` | `Dictionary<string, string?>` | Stored property |
| `Contacts` | `List<OrganizationContact>` | Stored property |
| `Settlements` | `List<OrganizationSettlement>` | Stored property |

## `OrganizationContact` - `WorkspaceCardData.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Stored property |
| `Position` | `string` | Stored property |
| `Email` | `string` | Stored property |
| `Mobile` | `string` | Stored property |
| `Phone` | `string` | Stored property |

## `OrganizationSettlement` - `WorkspaceCardData.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Date` | `DateTime` | Stored property |
| `Amount` | `decimal` | Stored property |
| `Note` | `string` | Stored property |
| `PaymentMethod` | `string?` | Stored property |
| `Reference` | `string?` | Stored property |
| `Currency` | `string` | Stored property |

## `OfficeOperation` - `OfficeOperation.cs`

| Property | C# type | Migration role |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Kind` | `string` | Stored property |
| `OccurredOn` | `DateTime` | Stored property |
| `Description` | `string` | Stored property |
| `Category` | `string?` | Stored property |
| `Amount` | `decimal` | Stored property |
| `PaymentMethod` | `string?` | Stored property |
| `Reference` | `string?` | Stored property |
| `Notes` | `string?` | Stored property |
| `PartnerId` | `Guid?` | Foreign key or linked ID |
| `CompanyId` | `Guid?` | Foreign key or linked ID |
| `GrossPremium` | `decimal?` | Stored property |
| `NetPremium` | `decimal?` | Stored property |
| `OfficeCommission` | `decimal?` | Stored property |
| `PartnerCommission` | `decimal?` | Stored property |
| `OverCommission` | `decimal?` | Stored property |
| `CreatedAtUtc` | `DateTime` | Audit metadata |
| `UpdatedAtUtc` | `DateTime` | Audit metadata |
| `CreatedByUserId` | `Guid?` | Audit metadata |
