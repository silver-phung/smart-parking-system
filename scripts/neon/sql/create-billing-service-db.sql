-- Run this file in the Neon SQL Editor on BILLING_SERVICE_DB.
-- Prepares the schema for Payment & Billing Microservice (PaymentNotificationService / VnPay integration).
BEGIN;

CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS public."PricingPolicies" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Version" integer NOT NULL,
    "ParkingLotId" uuid,
    "ParkingZoneId" uuid,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveTo" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "GracePeriodMinutes" integer NOT NULL,
    "DayBlockRate" numeric(18,2) NOT NULL,
    "EveningBlockRate" numeric(18,2) NOT NULL,
    "OvernightTouchRate" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PricingPolicies" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS public."ParkingSessions" (
    "Id" uuid NOT NULL,
    "LicensePlate" text NOT NULL,
    "VehicleType" integer NOT NULL,
    "CheckInTime" timestamp with time zone NOT NULL,
    "CheckOutTime" timestamp with time zone,
    "IsMonthlyPass" boolean NOT NULL,
    "AppliedPricingPolicyId" uuid,
    CONSTRAINT "PK_ParkingSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ParkingSessions_PricingPolicies_AppliedPricingPolicyId"
        FOREIGN KEY ("AppliedPricingPolicyId") REFERENCES public."PricingPolicies" ("Id")
);

CREATE TABLE IF NOT EXISTS public."Invoices" (
    "Id" uuid NOT NULL,
    "SessionId" uuid NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "Status" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "PaidAt" timestamp with time zone,
    CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Invoices_ParkingSessions_SessionId"
        FOREIGN KEY ("SessionId") REFERENCES public."ParkingSessions" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS public."PaymentTransactions" (
    "Id" uuid NOT NULL,
    "InvoiceId" uuid NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentMethod" text NOT NULL,
    "TransactionNo" text NOT NULL,
    "ResponseCode" text NOT NULL,
    "Status" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PaymentTransactions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PaymentTransactions_Invoices_InvoiceId"
        FOREIGN KEY ("InvoiceId") REFERENCES public."Invoices" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Invoices_SessionId" ON public."Invoices" ("SessionId");
CREATE INDEX IF NOT EXISTS "IX_ParkingSessions_AppliedPricingPolicyId" ON public."ParkingSessions" ("AppliedPricingPolicyId");
CREATE INDEX IF NOT EXISTS "IX_PaymentTransactions_InvoiceId" ON public."PaymentTransactions" ("InvoiceId");

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111063000_InitBillingDb', '10.0.14')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
