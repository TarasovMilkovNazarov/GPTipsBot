
CREATE DATABASE gptips
;

-- switch to gptips database
\c gptips;

CREATE TABLE users (
    Id BIGSERIAL PRIMARY KEY,
    FirstName TEXT NOT NULL,
    LastName TEXT,
    Message TEXT,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL,
    IsActive boolean
)
;

-- -- psql -U postgres
-- SELECT 'CREATE DATABASE gptips' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'gptips')/gexec

CREATE SEQUENCE messages_id_seq START 1;
CREATE SEQUENCE messages_contextid_seq START 1;

CREATE TABLE IF NOT EXISTS public.messages
(
    id bigint NOT NULL DEFAULT nextval('messages_id_seq'::regclass),
    userid bigint,
    replytoid bigint,
    chatid bigint,
    text text COLLATE pg_catalog."default" NOT NULL,
    role integer NOT NULL,
    createdat timestamp with time zone NOT NULL,
    contextid bigint DEFAULT nextval('messages_contextid_seq'::regclass),
    CONSTRAINT messages_pkey PRIMARY KEY (id),
    CONSTRAINT messages_replytoid_fkey FOREIGN KEY (replytoid)
        REFERENCES public.messages (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION,
    CONSTRAINT messages_userid_fkey FOREIGN KEY (userid)
        REFERENCES public.users (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)


-- Set id as telegramid
UPDATE users AS u
SET id = t.telegramid
FROM users AS t
WHERE u.id = t.id;

CREATE TABLE botsettings (
  id bigint REFERENCES users(id),
  language VARCHAR(255)
);