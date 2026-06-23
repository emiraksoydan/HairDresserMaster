-- Randevu sosyal paylaşım kaydı (kullanıcı + randevu → tek paylaşım)
CREATE TABLE IF NOT EXISTS "AppointmentSocialShares" (
    "Id" uuid NOT NULL,
    "AppointmentId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ContentType" integer NOT NULL,
    "ContentId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AppointmentSocialShares" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AppointmentSocialShares_Appointments_AppointmentId" FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppointmentSocialShares_AppointmentId_UserId"
    ON "AppointmentSocialShares" ("AppointmentId", "UserId");
