using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Baseline_FullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppNotifications",
                columns: table => new
                {
                    AppNotificationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotifications", x => x.AppNotificationId);
                });

            migrationBuilder.CreateTable(
                name: "BillingParties",
                columns: table => new
                {
                    BillingPartyId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingParties", x => x.BillingPartyId);
                });

            migrationBuilder.CreateTable(
                name: "CompletedServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    Customer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Service = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UsedQty = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQty = table.Column<int>(type: "INTEGER", nullable: false),
                    DateRequested = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateCompleted = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccountHolder = table.Column<string>(type: "TEXT", nullable: false),
                    JobAssignedTo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletedServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Company = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastContact = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InvoiceBy = table.Column<string>(type: "TEXT", nullable: false),
                    ChargeTo = table.Column<string>(type: "TEXT", nullable: false),
                    InvoiceByPartyIdsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    ChargeToPartyIdsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    Package = table.Column<string>(type: "TEXT", nullable: false),
                    PackageValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cosec = table.Column<bool>(type: "INTEGER", nullable: false),
                    DivisionGroupCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HasLoa = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoaHoldersJson = table.Column<string>(type: "TEXT", nullable: false),
                    MoiFormTemplateCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MoaFormTemplateCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MoaWorkflowTemplateCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MoiJson = table.Column<string>(type: "TEXT", nullable: false),
                    MoiApprovalJson = table.Column<string>(type: "TEXT", nullable: false),
                    MoaJson = table.Column<string>(type: "TEXT", nullable: false),
                    MoiApprovalMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "AllRequired"),
                    PurchasedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "DivisionGroups",
                columns: table => new
                {
                    DivisionGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MoaWorkflowTemplateCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DefaultMoiFormTemplateCode = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultMoaFormTemplateCode = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivisionGroups", x => x.DivisionGroupId);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplates",
                columns: table => new
                {
                    FormTemplateId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AddressedTo = table.Column<string>(type: "TEXT", nullable: false),
                    DivisionLabel = table.Column<string>(type: "TEXT", nullable: false),
                    IssuerEntity = table.Column<string>(type: "TEXT", nullable: false),
                    PackageServiceName = table.Column<string>(type: "TEXT", nullable: false),
                    FieldsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.FormTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetOtps",
                columns: table => new
                {
                    PasswordResetOtpId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.PasswordResetOtpId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ServicesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceQuantitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    QtyPerYear = table.Column<int>(type: "INTEGER", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AddOnsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AddOnQuantitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    AddOnsQty = table.Column<int>(type: "INTEGER", nullable: false),
                    AddOnPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTemplates",
                columns: table => new
                {
                    WorkflowTemplateId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTemplates", x => x.WorkflowTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPackages",
                columns: table => new
                {
                    CustomerPackageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PackageValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    PackageDetail = table.Column<string>(type: "TEXT", nullable: true),
                    Validity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PricingJson = table.Column<string>(type: "TEXT", nullable: false),
                    PurchasedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPackages", x => x.CustomerPackageId);
                    table.ForeignKey(
                        name: "FK_CustomerPackages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Mobile = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "User"),
                    JobTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CanRecommendMoi = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanApproveMoiIntake = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CanApproveMoi = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CanApproveMoa = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsInternalSignatory = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsVerified = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    InvitedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepTemplates",
                columns: table => new
                {
                    WorkflowStepTemplateId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StepKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConditionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AssigneeType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AssigneeRole = table.Column<string>(type: "TEXT", nullable: true),
                    AssigneeUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssigneeDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    AllowAdminOverride = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepTemplates", x => x.WorkflowStepTemplateId);
                    table.ForeignKey(
                        name: "FK_WorkflowStepTemplates_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "WorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequests",
                columns: table => new
                {
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerPackageId = table.Column<int>(type: "INTEGER", nullable: true),
                    Customer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Service = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UsedQty = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQty = table.Column<int>(type: "INTEGER", nullable: false),
                    DateRequested = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateCompleted = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccountHolder = table.Column<string>(type: "TEXT", nullable: false),
                    AccountHolderEmail = table.Column<string>(type: "TEXT", nullable: false),
                    AccountHolderPhone = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobAssignedTo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InternalHandoffStatus = table.Column<string>(type: "TEXT", nullable: false),
                    AssignmentComments = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequests", x => x.JobRequestId);
                    table.ForeignKey(
                        name: "FK_JobRequests_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "CustomerPackageId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PackageScheduleItems",
                columns: table => new
                {
                    PackageScheduleItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerPackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    BookingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageScheduleItems", x => x.PackageScheduleItemId);
                    table.ForeignKey(
                        name: "FK_PackageScheduleItems_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "CustomerPackageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageScheduleItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountHolders",
                columns: table => new
                {
                    AccountHolderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NeedsMoi = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsMoiApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsMoa = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientAdded = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountHolders", x => x.AccountHolderId);
                    table.ForeignKey(
                        name: "FK_AccountHolders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountHolders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DivisionGroupRecommenders",
                columns: table => new
                {
                    DivisionGroupRecommenderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DivisionGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivisionGroupRecommenders", x => x.DivisionGroupRecommenderId);
                    table.ForeignKey(
                        name: "FK_DivisionGroupRecommenders_DivisionGroups_DivisionGroupId",
                        column: x => x.DivisionGroupId,
                        principalTable: "DivisionGroups",
                        principalColumn: "DivisionGroupId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivisionGroupRecommenders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SignatoryCustomerAccess",
                columns: table => new
                {
                    SignatoryCustomerAccessId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatoryCustomerAccess", x => x.SignatoryCustomerAccessId);
                    table.ForeignKey(
                        name: "FK_SignatoryCustomerAccess_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignatoryCustomerAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequestUnits",
                columns: table => new
                {
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedUserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InternalHandoffStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PackageScheduleItemId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequestUnits", x => x.JobRequestUnitId);
                    table.ForeignKey(
                        name: "FK_JobRequestUnits_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceJobForms",
                columns: table => new
                {
                    ServiceJobFormId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    Service = table.Column<string>(type: "TEXT", nullable: false),
                    FormDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobForms", x => x.ServiceJobFormId);
                    table.ForeignKey(
                        name: "FK_ServiceJobForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobItemDocuments",
                columns: table => new
                {
                    JobItemDocumentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedByName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VisibleToInternal = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobItemDocuments", x => x.JobItemDocumentId);
                    table.ForeignKey(
                        name: "FK_JobItemDocuments_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId");
                    table.ForeignKey(
                        name: "FK_JobItemDocuments_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequestUnitAssignees",
                columns: table => new
                {
                    JobRequestUnitAssigneeId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequestUnitAssignees", x => x.JobRequestUnitAssigneeId);
                    table.ForeignKey(
                        name: "FK_JobRequestUnitAssignees_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRequestUnitAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MOIForms",
                columns: table => new
                {
                    MOIFormId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    FormDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    FormTemplateCode = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowState = table.Column<string>(type: "TEXT", nullable: false),
                    FinanceRelated = table.Column<bool>(type: "INTEGER", nullable: false),
                    BankSignatoryMatter = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecommendedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecommendedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecommendationComments = table.Column<string>(type: "TEXT", nullable: false),
                    ClientApprovalsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RejectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOIForms", x => x.MOIFormId);
                    table.ForeignKey(
                        name: "FK_MOIForms_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOIForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MOAForms",
                columns: table => new
                {
                    MOAFormId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    MOIFormId = table.Column<int>(type: "INTEGER", nullable: true),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    FormDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    FormTemplateCode = table.Column<string>(type: "TEXT", nullable: false),
                    FinanceRelated = table.Column<bool>(type: "INTEGER", nullable: false),
                    BankSignatoryMatter = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShareMovement = table.Column<bool>(type: "INTEGER", nullable: false),
                    PackChecklistJson = table.Column<string>(type: "TEXT", nullable: false),
                    ClientApprovalsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RejectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SharonApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmittedForAdminReviewAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOAForms", x => x.MOAFormId);
                    table.ForeignKey(
                        name: "FK_MOAForms_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOAForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOAForms_MOIForms_MOIFormId",
                        column: x => x.MOIFormId,
                        principalTable: "MOIForms",
                        principalColumn: "MOIFormId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    WorkflowInstanceId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    FormType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    MoiFormId = table.Column<int>(type: "INTEGER", nullable: true),
                    MoaFormId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.WorkflowInstanceId);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_MOAForms_MoaFormId",
                        column: x => x.MoaFormId,
                        principalTable: "MOAForms",
                        principalColumn: "MOAFormId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_MOIForms_MoiFormId",
                        column: x => x.MoiFormId,
                        principalTable: "MOIForms",
                        principalColumn: "MOIFormId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "WorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepInstances",
                columns: table => new
                {
                    WorkflowStepInstanceId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowInstanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StepKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConditionType = table.Column<string>(type: "TEXT", nullable: false),
                    AssigneeType = table.Column<string>(type: "TEXT", nullable: false),
                    AssigneeUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssigneeName = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Comments = table.Column<string>(type: "TEXT", nullable: false),
                    AdminOverridden = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverriddenByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepInstances", x => x.WorkflowStepInstanceId);
                    table.ForeignKey(
                        name: "FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "WorkflowInstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountHolders_CustomerId",
                table: "AccountHolders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHolders_UserId",
                table: "AccountHolders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId_IsRead",
                table: "AppNotifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_CustomerId",
                table: "CustomerPackages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroupRecommenders_DivisionGroupId",
                table: "DivisionGroupRecommenders",
                column: "DivisionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroupRecommenders_UserId",
                table: "DivisionGroupRecommenders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroups_Code",
                table: "DivisionGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_FormType_Code",
                table: "FormTemplates",
                columns: new[] { "FormType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobItemDocuments_JobRequestId",
                table: "JobItemDocuments",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_JobItemDocuments_JobRequestUnitId",
                table: "JobItemDocuments",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_CustomerId",
                table: "JobRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_CustomerPackageId",
                table: "JobRequests",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnitAssignees_JobRequestUnitId_UserId",
                table: "JobRequestUnitAssignees",
                columns: new[] { "JobRequestUnitId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnitAssignees_UserId",
                table: "JobRequestUnitAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnits_JobRequestId_UnitNumber",
                table: "JobRequestUnits",
                columns: new[] { "JobRequestId", "UnitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_JobRequestId",
                table: "MOAForms",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_JobRequestUnitId",
                table: "MOAForms",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_MOIFormId",
                table: "MOAForms",
                column: "MOIFormId");

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_JobRequestId",
                table: "MOIForms",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_JobRequestUnitId",
                table: "MOIForms",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageScheduleItems_CustomerId",
                table: "PackageScheduleItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageScheduleItems_CustomerPackageId",
                table: "PackageScheduleItems",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_Email_CreatedAt",
                table: "PasswordResetOtps",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobForms_JobRequestId",
                table: "ServiceJobForms",
                column: "JobRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatoryCustomerAccess_CustomerId",
                table: "SignatoryCustomerAccess",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatoryCustomerAccess_UserId_CustomerId",
                table: "SignatoryCustomerAccess",
                columns: new[] { "UserId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerId",
                table: "Users",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_MoaFormId",
                table: "WorkflowInstances",
                column: "MoaFormId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_MoiFormId",
                table: "WorkflowInstances",
                column: "MoiFormId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowTemplateId",
                table: "WorkflowInstances",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_WorkflowInstanceId",
                table: "WorkflowStepInstances",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepTemplates_WorkflowTemplateId",
                table: "WorkflowStepTemplates",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTemplates_Code",
                table: "WorkflowTemplates",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountHolders");

            migrationBuilder.DropTable(
                name: "AppNotifications");

            migrationBuilder.DropTable(
                name: "BillingParties");

            migrationBuilder.DropTable(
                name: "CompletedServices");

            migrationBuilder.DropTable(
                name: "DivisionGroupRecommenders");

            migrationBuilder.DropTable(
                name: "FormTemplates");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "JobItemDocuments");

            migrationBuilder.DropTable(
                name: "JobRequestUnitAssignees");

            migrationBuilder.DropTable(
                name: "PackageScheduleItems");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ServiceJobForms");

            migrationBuilder.DropTable(
                name: "SignatoryCustomerAccess");

            migrationBuilder.DropTable(
                name: "WorkflowStepInstances");

            migrationBuilder.DropTable(
                name: "WorkflowStepTemplates");

            migrationBuilder.DropTable(
                name: "DivisionGroups");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "MOAForms");

            migrationBuilder.DropTable(
                name: "WorkflowTemplates");

            migrationBuilder.DropTable(
                name: "MOIForms");

            migrationBuilder.DropTable(
                name: "JobRequestUnits");

            migrationBuilder.DropTable(
                name: "JobRequests");

            migrationBuilder.DropTable(
                name: "CustomerPackages");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
