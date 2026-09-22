
-- Drop tables if they already exist 
IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

  -- TABLE: Users

CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100)   NOT NULL,
    Email           NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255)   NOT NULL,
    Role            NVARCHAR(20)    NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);
GO

   --TABLE: Events

CREATE TABLE Events (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT             NOT NULL,
    Name            NVARCHAR(150)   NOT NULL,
    Description     NVARCHAR(MAX)   NULL,
    EventDate       DATETIME        NOT NULL,
    Location        NVARCHAR(150)   NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);
GO

   --TABLE: Categories
CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT             NOT NULL,
    Name            NVARCHAR(100)   NOT NULL,
    Distance        DECIMAL(6,2)    NOT NULL,
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events(EventId)
);
GO

  -- TABLE: Enrolments

CREATE TABLE Enrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO

  -- TABLE: Results

CREATE TABLE Results (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    FinishTime      TIME            NULL,
    Position        INT             NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Not Started',
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);
GO

  -- TABLE: Payments

CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    Amount          DECIMAL(8,2)    NOT NULL,
    PaymentDate     DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Payments_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);
GO

  -- SEED DATA

-- Organisers (2)
INSERT INTO Users (FullName, Email, PasswordHash, Role) VALUES
('John Naidoo', 'john.naidoo@raceday.co.za', 'HASHED_PASSWORD_1', 'Organiser'),
('Sarah van der Merwe', 'sarah.vdm@raceday.co.za', 'HASHED_PASSWORD_2', 'Organiser');

-- Participants (2)
INSERT INTO Users (FullName, Email, PasswordHash, Role) VALUES
('Thabo Mokoena', 'thabo.mokoena@gmail.com', 'HASHED_PASSWORD_3', 'Participant'),
('Lindiwe Zulu', 'lindiwe.zulu@gmail.com', 'HASHED_PASSWORD_4', 'Participant');

-- Events (3)
INSERT INTO Events (OrganiserId, Name, Description, EventDate, Location) VALUES
(1, 'Comrades Marathon 2026', 'The iconic ultramarathon between Pietermaritzburg and Durban.', '2026-06-14 05:30:00', 'Pietermaritzburg, KwaZulu-Natal'),
(1, 'Cape Town Cycle Tour 2026', 'The world''s largest individually timed cycle race.', '2026-03-08 06:00:00', 'Cape Town, Western Cape'),
(2, 'Soweto Marathon 2026', 'A community road running event through the streets of Soweto.', '2026-11-01 06:00:00', 'Soweto, Gauteng');

-- Categories (2 per event)
INSERT INTO Categories (EventId, Name, Distance, EntryFee) VALUES
(1, 'Up Run 90km', 90.00, 950.00),
(1, 'Novice Support 56km', 56.00, 650.00),
(2, '109km Individual Time Trial', 109.00, 850.00),
(2, 'Mini Cycle 42km', 42.00, 350.00),
(3, '10km Fun Run', 10.00, 150.00),
(3, 'Half Marathon 21km', 21.10, 300.00);

-- Enrolments 
INSERT INTO Enrolments (ParticipantId, CategoryId, Status) VALUES
(3, 1, 'Confirmed'),   -- Thabo entered the Comrades Up Run
(3, 5, 'Confirmed'),   -- Thabo also entered the Soweto 10km
(4, 3, 'Confirmed'),   -- Lindiwe entered the Cape Town Cycle Tour
(4, 6, 'Pending');     -- Lindiwe entered the Soweto Half Marathon, payment pending

-- Results 
INSERT INTO Results (EnrolmentId, FinishTime, Position, Status) VALUES
(1, '08:45:12', 152, 'Finished'),
(3, '03:12:45', 40, 'Finished');

-- Payments 
INSERT INTO Payments (EnrolmentId, Amount, Status) VALUES
(1, 950.00, 'Paid'),
(2, 150.00, 'Paid'),
(3, 850.00, 'Paid');
GO

SELECT * FROM Users;
SELECT * FROM Events;
SELECT * FROM Categories;
SELECT * FROM Enrolments;
SELECT * FROM Results;
SELECT * FROM Payments;