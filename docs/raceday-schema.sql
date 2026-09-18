create schema RaceDay;
use  RaceDay;
drop schema RaceDay;
DROP TABLE IF EXISTS Payments;
DROP TABLE IF EXISTS Results;
DROP TABLE IF EXISTS Enrolments;
DROP TABLE IF EXISTS Categories;
DROP TABLE IF EXISTS Events;
DROP TABLE IF EXISTS Users;

-- TABLE: User
CREATE TABLE Users (
    UserId          INT AUTO_INCREMENT PRIMARY KEY,
    FullName        VARCHAR(100)    NOT NULL,
    Email           VARCHAR(150)    NOT NULL UNIQUE,
    PasswordHash    VARCHAR(255)    NOT NULL,
    Role            VARCHAR(20)     NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);

-- TABLE: Events
CREATE TABLE Events (
    EventId         INT AUTO_INCREMENT PRIMARY KEY,
    OrganiserId     INT             NOT NULL,
    Name            VARCHAR(150)    NOT NULL,
    Description     TEXT            NULL,
    EventDate       DATETIME        NOT NULL,
    Location        VARCHAR(150)    NOT NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);

-- TABLE: Categories
CREATE TABLE Categories (
    CategoryId      INT AUTO_INCREMENT PRIMARY KEY,
    EventId         INT             NOT NULL,
    Name            VARCHAR(100)    NOT NULL,
    Distance        DECIMAL(6,2)    NOT NULL,
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES Events(EventId)
);

-- TABLE: Enrolments
CREATE TABLE Enrolments (
    EnrolmentId     INT AUTO_INCREMENT PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Status          VARCHAR(20)     NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);

-- TABLE: Results
CREATE TABLE Results (
    ResultId        INT AUTO_INCREMENT PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    FinishTime      TIME            NULL,
    Position        INT             NULL,
    Status          VARCHAR(20)     NOT NULL DEFAULT 'Not Started',
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);

-- TABLE: Payments
CREATE TABLE Payments (
    PaymentId       INT AUTO_INCREMENT PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    Amount          DECIMAL(8,2)    NOT NULL,
    PaymentDate     DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Status          VARCHAR(20)     NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Payments_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);

-- SEED DATA
-- Sample data used was generated with Claude AI 

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
(1, 'Cape Town Cycle Tour 2026', 'The world\'s largest individually timed cycle race.', '2026-03-08 06:00:00', 'Cape Town, Western Cape'),
(2, 'Soweto Marathon 2026', 'A community road running event through the streets of Soweto.', '2026-11-01 06:00:00', 'Soweto, Gauteng');

-- Categories (2 per event)
INSERT INTO Categories (EventId, Name, Distance, EntryFee) VALUES
(1, 'Up Run 90km', 90.00, 950.00),
(1, 'Novice Support 56km', 56.00, 650.00),
(2, '109km Individual Time Trial', 109.00, 850.00),
(2, 'Mini Cycle 42km', 42.00, 350.00),
(3, '10km Fun Run', 10.00, 150.00),
(3, 'Half Marathon 21km', 21.10, 300.00);

-- Enrolments (sample)
INSERT INTO Enrolments (ParticipantId, CategoryId, Status) VALUES
(3, 1, 'Confirmed'),
(3, 5, 'Confirmed'),
(4, 3, 'Confirmed'),
(4, 6, 'Pending');

-- Results (sample, for completed enrolments)
INSERT INTO Results (EnrolmentId, FinishTime, Position, Status) VALUES
(1, '08:45:12', 152, 'Finished'),
(3, '03:12:45', 40, 'Finished');

-- Payments (sample)
INSERT INTO Payments (EnrolmentId, Amount, Status) VALUES
(1, 950.00, 'Paid'),
(2, 150.00, 'Paid'),
(3, 850.00, 'Paid');

SELECT * FROM Users;
SELECT * FROM Events;
SELECT * FROM Categories;
SELECT * FROM Enrolments;
SELECT * FROM Results;
SELECT * FROM Payments;
