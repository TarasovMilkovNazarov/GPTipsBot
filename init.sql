
CREATE DATABASE gptips
;

-- switch to gptips database
\c gptips;

CREATE TABLE users (
    Id BIGSERIAL PRIMARY KEY,
    FirstName TEXT NOT NULL,
    LastName TEXT,
    TelegramId BIGINT NOT NULL,
    Message TEXT,
    TimeStamp TIMESTAMP WITH TIME ZONE NOT NULL,
    IsActive boolean
)
;

-- -- psql -U postgres
-- SELECT 'CREATE DATABASE gptips' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'gptips')/gexec

CREATE TABLE messages (
    Id BIGSERIAL PRIMARY KEY,
    UserId BIGINT,
    Text TEXT NOT NULL,
    TelegramId BIGINT NOT NULL,
    TimeStamp TIMESTAMP WITH TIME ZONE NOT NULL,
    FOREIGN KEY (UserId) REFERENCES users(Id)
);