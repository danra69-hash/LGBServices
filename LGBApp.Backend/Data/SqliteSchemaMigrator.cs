using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Data;

public static class SqliteSchemaMigrator
{
    public static void Apply(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "CustomerPackages" (
                "CustomerPackageId" INTEGER NOT NULL CONSTRAINT "PK_CustomerPackages" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NOT NULL,
                "PackageName" TEXT NOT NULL,
                "PackageValue" TEXT NOT NULL,
                "PackageDetail" TEXT NULL,
                "PurchasedDate" TEXT NOT NULL,
                "ExpiryDate" TEXT NOT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Active',
                CONSTRAINT "FK_CustomerPackages_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            INSERT INTO "CustomerPackages" ("CustomerId", "PackageName", "PackageValue", "PackageDetail", "PurchasedDate", "ExpiryDate", "Status")
            SELECT c."CustomerId", c."Package", c."PackageValue", NULL, c."PurchasedDate", c."ExpiryDate", 'Active'
            FROM "Customers" c
            WHERE c."Package" != ''
              AND NOT EXISTS (
                  SELECT 1 FROM "CustomerPackages" cp WHERE cp."CustomerId" = c."CustomerId"
              );
            """);

        EnsureColumn(context, "CustomerPackages", "Validity", "TEXT NOT NULL DEFAULT '1 Year'");
        EnsureColumn(context, "CustomerPackages", "PricingJson", "TEXT NOT NULL DEFAULT '{}'");

        EnsureColumn(context, "JobRequests", "CustomerId", "INTEGER NULL");
        EnsureColumn(context, "JobRequests", "CustomerPackageId", "INTEGER NULL");
        EnsureColumn(context, "JobRequests", "ScheduledDate", "TEXT NULL");
        EnsureColumn(context, "JobRequests", "TaskType", "TEXT NOT NULL DEFAULT 'Service'");
        EnsureColumn(context, "JobRequests", "AssignedUserId", "INTEGER NULL");
        EnsureColumn(context, "JobRequests", "AccountHolderEmail", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "JobRequests", "AccountHolderPhone", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "JobRequests", "InternalHandoffStatus", "TEXT NOT NULL DEFAULT ''");

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobRequestUnits" (
                "JobRequestUnitId" INTEGER NOT NULL CONSTRAINT "PK_JobRequestUnits" PRIMARY KEY AUTOINCREMENT,
                "JobRequestId" INTEGER NOT NULL,
                "UnitNumber" INTEGER NOT NULL,
                "AssignedUserId" INTEGER NULL,
                "AssignedUserName" TEXT NOT NULL DEFAULT '',
                "ScheduledDate" TEXT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Pending',
                "CompletedAt" TEXT NULL,
                "PackageScheduleItemId" INTEGER NULL,
                CONSTRAINT "FK_JobRequestUnits_JobRequests_JobRequestId" FOREIGN KEY ("JobRequestId") REFERENCES "JobRequests" ("JobRequestId") ON DELETE CASCADE
            );
            """);

        EnsureColumn(context, "PackageScheduleItems", "JobRequestUnitId", "INTEGER NULL");
        EnsureColumn(context, "PackageScheduleItems", "AssignedUserId", "INTEGER NULL");

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobRequestUnitAssignees" (
                "JobRequestUnitAssigneeId" INTEGER NOT NULL CONSTRAINT "PK_JobRequestUnitAssignees" PRIMARY KEY AUTOINCREMENT,
                "JobRequestUnitId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                CONSTRAINT "FK_JobRequestUnitAssignees_JobRequestUnits_JobRequestUnitId" FOREIGN KEY ("JobRequestUnitId") REFERENCES "JobRequestUnits" ("JobRequestUnitId") ON DELETE CASCADE,
                CONSTRAINT "FK_JobRequestUnitAssignees_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_JobRequestUnitAssignees_JobRequestUnitId_UserId"
            ON "JobRequestUnitAssignees" ("JobRequestUnitId", "UserId");
            """);

        context.Database.ExecuteSqlRaw("""
            INSERT INTO "JobRequestUnitAssignees" ("JobRequestUnitId", "UserId")
            SELECT u."JobRequestUnitId", u."AssignedUserId"
            FROM "JobRequestUnits" u
            WHERE u."AssignedUserId" IS NOT NULL
              AND EXISTS (SELECT 1 FROM "Users" us WHERE us."UserId" = u."AssignedUserId")
              AND NOT EXISTS (
                  SELECT 1 FROM "JobRequestUnitAssignees" a
                  WHERE a."JobRequestUnitId" = u."JobRequestUnitId" AND a."UserId" = u."AssignedUserId"
              );
            """);

        EnsureColumn(context, "Customers", "DivisionGroupCode", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "Customers", "HasLoa", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Customers", "LoaHoldersJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(context, "Customers", "MoiFormTemplateCode", "TEXT NULL");
        EnsureColumn(context, "Customers", "MoaFormTemplateCode", "TEXT NULL");
        EnsureColumn(context, "Customers", "MoaWorkflowTemplateCode", "TEXT NULL");

        EnsureColumn(context, "Users", "JobTitle", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "Users", "CanRecommendMoi", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Users", "CustomerId", "INTEGER NULL");
        EnsureColumn(context, "Users", "InvitedByUserId", "INTEGER NULL");
        EnsureColumn(context, "Users", "MustChangePassword", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(context, "Users", "CanApproveMoiIntake", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Users", "CanApproveMoi", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Users", "CanApproveMoa", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Users", "IsInternalSignatory", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "Customers", "InvoiceByPartyIdsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(context, "Customers", "ChargeToPartyIdsJson", "TEXT NOT NULL DEFAULT '[]'");

        context.Database.ExecuteSqlRaw("""
            UPDATE "Users" SET "Role" = 'ClientAdmin' WHERE "Role" = 'Client';
            """);

        EnsureColumn(context, "AccountHolders", "NeedsMoi", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "AccountHolders", "NeedsMoiApproval", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "AccountHolders", "NeedsMoa", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "AccountHolders", "UserId", "INTEGER NULL");
        EnsureColumn(context, "AccountHolders", "ClientAdded", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "AccountHolders", "AddedByUserId", "INTEGER NULL");

        EnsureColumn(context, "FormTemplates", "PackageServiceName", "TEXT NOT NULL DEFAULT ''");

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "BillingParties" (
                "BillingPartyId" INTEGER NOT NULL CONSTRAINT "PK_BillingParties" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Category" TEXT NOT NULL DEFAULT 'Both',
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ServiceJobForms" (
                "ServiceJobFormId" INTEGER NOT NULL CONSTRAINT "PK_ServiceJobForms" PRIMARY KEY AUTOINCREMENT,
                "JobRequestId" INTEGER NOT NULL,
                "Company" TEXT NOT NULL DEFAULT '',
                "Service" TEXT NOT NULL DEFAULT '',
                "FormDataJson" TEXT NOT NULL DEFAULT '{{}}',
                "Status" TEXT NOT NULL DEFAULT 'Draft',
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_ServiceJobForms_JobRequests_JobRequestId" FOREIGN KEY ("JobRequestId") REFERENCES "JobRequests" ("JobRequestId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceJobForms_JobRequestId" ON "ServiceJobForms" ("JobRequestId");
            """);

        EnsureColumn(context, "MOIForms", "FormTemplateCode", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "MOIForms", "WorkflowState", "TEXT NOT NULL DEFAULT 'Draft'");
        EnsureColumn(context, "MOIForms", "FinanceRelated", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "MOIForms", "BankSignatoryMatter", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "MOIForms", "RecommendedByUserId", "INTEGER NULL");
        EnsureColumn(context, "MOIForms", "RecommendedAt", "TEXT NULL");
        EnsureColumn(context, "MOIForms", "RecommendationComments", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "MOIForms", "ClientApprovalsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(context, "MOIForms", "RejectionsJson", "TEXT NOT NULL DEFAULT '[]'");

        EnsureColumn(context, "MOAForms", "FormTemplateCode", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(context, "MOAForms", "FinanceRelated", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "MOAForms", "BankSignatoryMatter", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "MOAForms", "ShareMovement", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(context, "MOAForms", "PackChecklistJson", "TEXT NOT NULL DEFAULT '{}'");
        EnsureColumn(context, "MOAForms", "ClientApprovalsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(context, "MOAForms", "RejectionsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureColumn(context, "MOAForms", "SharonApprovedAt", "TEXT NULL");
        EnsureColumn(context, "MOAForms", "SubmittedForAdminReviewAt", "TEXT NULL");
        EnsureColumn(context, "MOAForms", "JobRequestId", "INTEGER NULL");

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "DivisionGroups" (
                "DivisionGroupId" INTEGER NOT NULL CONSTRAINT "PK_DivisionGroups" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "MoaWorkflowTemplateCode" TEXT NOT NULL DEFAULT 'MOA_NO_LOA',
                "DefaultMoiFormTemplateCode" TEXT NULL,
                "DefaultMoaFormTemplateCode" TEXT NULL,
                "IsActive" INTEGER NOT NULL DEFAULT 1
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DivisionGroups_Code" ON "DivisionGroups" ("Code");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "DivisionGroupRecommenders" (
                "DivisionGroupRecommenderId" INTEGER NOT NULL CONSTRAINT "PK_DivisionGroupRecommenders" PRIMARY KEY AUTOINCREMENT,
                "DivisionGroupId" INTEGER NOT NULL,
                "UserId" INTEGER NULL,
                "DisplayName" TEXT NOT NULL DEFAULT '',
                CONSTRAINT "FK_DivisionGroupRecommenders_DivisionGroups_DivisionGroupId" FOREIGN KEY ("DivisionGroupId") REFERENCES "DivisionGroups" ("DivisionGroupId") ON DELETE CASCADE,
                CONSTRAINT "FK_DivisionGroupRecommenders_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE SET NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "FormTemplates" (
                "FormTemplateId" INTEGER NOT NULL CONSTRAINT "PK_FormTemplates" PRIMARY KEY AUTOINCREMENT,
                "FormType" TEXT NOT NULL,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL DEFAULT '',
                "AddressedTo" TEXT NOT NULL DEFAULT '',
                "DivisionLabel" TEXT NOT NULL DEFAULT '',
                "IssuerEntity" TEXT NOT NULL DEFAULT '',
                "FieldsJson" TEXT NOT NULL DEFAULT '[]',
                "IsDefault" INTEGER NOT NULL DEFAULT 0,
                "IsActive" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_FormTemplates_FormType_Code" ON "FormTemplates" ("FormType", "Code");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "WorkflowTemplates" (
                "WorkflowTemplateId" INTEGER NOT NULL CONSTRAINT "PK_WorkflowTemplates" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "WorkflowType" TEXT NOT NULL DEFAULT 'MOA',
                "Description" TEXT NOT NULL DEFAULT '',
                "IsActive" INTEGER NOT NULL DEFAULT 1
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WorkflowTemplates_Code" ON "WorkflowTemplates" ("Code");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "WorkflowStepTemplates" (
                "WorkflowStepTemplateId" INTEGER NOT NULL CONSTRAINT "PK_WorkflowStepTemplates" PRIMARY KEY AUTOINCREMENT,
                "WorkflowTemplateId" INTEGER NOT NULL,
                "StepOrder" INTEGER NOT NULL,
                "StepKey" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "ConditionType" TEXT NOT NULL DEFAULT 'Always',
                "AssigneeType" TEXT NOT NULL DEFAULT 'JobTitle',
                "AssigneeRole" TEXT NULL,
                "AssigneeUserId" INTEGER NULL,
                "AssigneeDisplayName" TEXT NULL,
                "AllowAdminOverride" INTEGER NOT NULL DEFAULT 1,
                CONSTRAINT "FK_WorkflowStepTemplates_WorkflowTemplates_WorkflowTemplateId" FOREIGN KEY ("WorkflowTemplateId") REFERENCES "WorkflowTemplates" ("WorkflowTemplateId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "WorkflowInstances" (
                "WorkflowInstanceId" INTEGER NOT NULL CONSTRAINT "PK_WorkflowInstances" PRIMARY KEY AUTOINCREMENT,
                "WorkflowTemplateId" INTEGER NOT NULL,
                "FormType" TEXT NOT NULL,
                "MoiFormId" INTEGER NULL,
                "MoaFormId" INTEGER NULL,
                "Status" TEXT NOT NULL DEFAULT 'Active',
                "CurrentStepOrder" INTEGER NOT NULL DEFAULT 1,
                "ConditionsJson" TEXT NOT NULL DEFAULT '{{}}',
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_WorkflowInstances_WorkflowTemplates_WorkflowTemplateId" FOREIGN KEY ("WorkflowTemplateId") REFERENCES "WorkflowTemplates" ("WorkflowTemplateId"),
                CONSTRAINT "FK_WorkflowInstances_MOIForms_MoiFormId" FOREIGN KEY ("MoiFormId") REFERENCES "MOIForms" ("MOIFormId") ON DELETE CASCADE,
                CONSTRAINT "FK_WorkflowInstances_MOAForms_MoaFormId" FOREIGN KEY ("MoaFormId") REFERENCES "MOAForms" ("MOAFormId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "WorkflowStepInstances" (
                "WorkflowStepInstanceId" INTEGER NOT NULL CONSTRAINT "PK_WorkflowStepInstances" PRIMARY KEY AUTOINCREMENT,
                "WorkflowInstanceId" INTEGER NOT NULL,
                "StepOrder" INTEGER NOT NULL,
                "StepKey" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "ConditionType" TEXT NOT NULL DEFAULT 'Always',
                "AssigneeType" TEXT NOT NULL DEFAULT '',
                "AssigneeUserId" INTEGER NULL,
                "AssigneeName" TEXT NOT NULL DEFAULT '',
                "Status" TEXT NOT NULL DEFAULT 'Pending',
                "ApprovedByUserId" INTEGER NULL,
                "ApprovedAt" TEXT NULL,
                "Comments" TEXT NOT NULL DEFAULT '',
                "AdminOverridden" INTEGER NOT NULL DEFAULT 0,
                "OverriddenByUserId" INTEGER NULL,
                CONSTRAINT "FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId" FOREIGN KEY ("WorkflowInstanceId") REFERENCES "WorkflowInstances" ("WorkflowInstanceId") ON DELETE CASCADE
            );
            """);

        EnsureColumn(context, "Customers", "MoiApprovalMode", "TEXT NOT NULL DEFAULT 'AllRequired'");
        EnsureColumn(context, "MOIForms", "JobRequestUnitId", "INTEGER NULL");
        EnsureColumn(context, "MOAForms", "JobRequestUnitId", "INTEGER NULL");
        EnsureColumn(context, "JobRequestUnits", "InternalHandoffStatus", "TEXT NOT NULL DEFAULT ''");

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "JobItemDocuments" (
                "JobItemDocumentId" INTEGER NOT NULL CONSTRAINT "PK_JobItemDocuments" PRIMARY KEY AUTOINCREMENT,
                "JobRequestId" INTEGER NOT NULL,
                "JobRequestUnitId" INTEGER NULL,
                "Folder" TEXT NOT NULL DEFAULT 'supporting',
                "FileName" TEXT NOT NULL DEFAULT '',
                "StorageKey" TEXT NOT NULL DEFAULT '',
                "ContentType" TEXT NOT NULL DEFAULT 'application/octet-stream',
                "UploadedByUserId" INTEGER NOT NULL DEFAULT 0,
                "UploadedByName" TEXT NOT NULL DEFAULT '',
                "UploadedAt" TEXT NOT NULL,
                "VisibleToInternal" INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT "FK_JobItemDocuments_JobRequests_JobRequestId" FOREIGN KEY ("JobRequestId") REFERENCES "JobRequests" ("JobRequestId") ON DELETE CASCADE
            );
            """);

        EnsureColumn(context, "JobItemDocuments", "JobRequestUnitId", "INTEGER NULL");

        context.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_JobItemDocuments_JobRequestId_Folder"
            ON "JobItemDocuments" ("JobRequestId", "Folder");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PackageScheduleItems" (
                "PackageScheduleItemId" INTEGER NOT NULL CONSTRAINT "PK_PackageScheduleItems" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NOT NULL,
                "CustomerPackageId" INTEGER NOT NULL,
                "ItemType" TEXT NOT NULL DEFAULT 'call',
                "Title" TEXT NOT NULL,
                "ScheduledAt" TEXT NOT NULL,
                "DurationMinutes" INTEGER NULL,
                "Status" TEXT NOT NULL DEFAULT 'scheduled',
                "Notes" TEXT NULL,
                "BookingUrl" TEXT NULL,
                "SequenceNumber" INTEGER NULL,
                CONSTRAINT "FK_PackageScheduleItems_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE CASCADE,
                CONSTRAINT "FK_PackageScheduleItems_CustomerPackages_CustomerPackageId" FOREIGN KEY ("CustomerPackageId") REFERENCES "CustomerPackages" ("CustomerPackageId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "SignatoryCustomerAccess" (
                "SignatoryCustomerAccessId" INTEGER NOT NULL CONSTRAINT "PK_SignatoryCustomerAccess" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "CustomerId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_SignatoryCustomerAccess_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE,
                CONSTRAINT "FK_SignatoryCustomerAccess_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SignatoryCustomerAccess_UserId_CustomerId"
            ON "SignatoryCustomerAccess" ("UserId", "CustomerId");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "AppNotifications" (
                "AppNotificationId" INTEGER NOT NULL CONSTRAINT "PK_AppNotifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "EventType" TEXT NOT NULL DEFAULT '',
                "Title" TEXT NOT NULL DEFAULT '',
                "Message" TEXT NOT NULL DEFAULT '',
                "JobRequestId" INTEGER NULL,
                "CustomerId" INTEGER NULL,
                "IsRead" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_AppNotifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Invoices" (
                "InvoiceId" INTEGER NOT NULL CONSTRAINT "PK_Invoices" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NOT NULL,
                "JobRequestId" INTEGER NULL,
                "InvoiceNumber" TEXT NOT NULL DEFAULT '',
                "Amount" REAL NOT NULL DEFAULT 0,
                "Currency" TEXT NOT NULL DEFAULT 'MYR',
                "Status" TEXT NOT NULL DEFAULT 'Draft',
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "IssuedAt" TEXT NULL,
                CONSTRAINT "FK_Invoices_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Invoices_InvoiceNumber"
            ON "Invoices" ("InvoiceNumber");
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PasswordResetOtps" (
                "PasswordResetOtpId" INTEGER NOT NULL CONSTRAINT "PK_PasswordResetOtps" PRIMARY KEY AUTOINCREMENT,
                "Email" TEXT NOT NULL,
                "CodeHash" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL,
                "ConsumedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "AttemptCount" INTEGER NOT NULL DEFAULT 0
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_PasswordResetOtps_Email_CreatedAt"
            ON "PasswordResetOtps" ("Email", "CreatedAt");
            """);
    }

    private static void EnsureColumn(AppDbContext context, string table, string column, string definition)
    {
        if (!TableExists(context, table) || ColumnExists(context, table, column))
            return;

        // Brace-doubling: ExecuteSqlRaw treats { } as format placeholders
        var escaped = definition.Replace("{", "{{").Replace("}", "}}");
        context.Database.ExecuteSqlRaw($"""ALTER TABLE "{table}" ADD COLUMN "{column}" {escaped};""");
    }

    private static bool TableExists(AppDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT 1 FROM sqlite_master
                WHERE type = 'table' AND name = $name
                LIMIT 1;
                """;
            var param = command.CreateParameter();
            param.ParameterName = "$name";
            param.Value = table;
            command.Parameters.Add(param);
            return command.ExecuteScalar() != null;
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }

    private static bool ColumnExists(AppDbContext context, string table, string column)
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally
        {
            if (!wasOpen)
                connection.Close();
        }
    }
}
