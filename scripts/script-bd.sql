-- =====================================================================================
-- Conserta Aí — API .NET — DDL do banco relacional (Azure SQL / SQL Server)
-- Tabelas de negócio com CRUD: APOIO e COMENTARIO (+ ETIQUETA e a junção N:N COMENTARIO_ETIQUETA)
-- Tabela técnica: IDEMPOTENCY_KEY (respostas armazenadas de POSTs idempotentes)
-- Gerado a partir do modelo do EF Core:  EF_PROVEDOR=SqlServer dotnet ef dbcontext script
-- A API também cria este schema sozinha ao iniciar (BancoDados:Provedor=SqlServer → EnsureCreated).
-- Versão Oracle (via Migrations): scripts/script-bd-oracle.sql
-- =====================================================================================

CREATE TABLE [APOIO] (
    [ID] uniqueidentifier NOT NULL,
    [ID_OCORRENCIA] bigint NOT NULL,
    [ID_USUARIO] nvarchar(64) NOT NULL,
    [DT_APOIO] datetime2 NOT NULL,
    CONSTRAINT [PK_APOIO] PRIMARY KEY ([ID])
);
GO


CREATE TABLE [COMENTARIO] (
    [ID] uniqueidentifier NOT NULL,
    [ID_OCORRENCIA] bigint NOT NULL,
    [ID_USUARIO] nvarchar(64) NOT NULL,
    [TEXTO] nvarchar(1000) NOT NULL,
    [DT_CRIACAO] datetime2 NOT NULL,
    [DT_EDICAO] datetime2 NULL,
    CONSTRAINT [PK_COMENTARIO] PRIMARY KEY ([ID])
);
GO


CREATE TABLE [ETIQUETA] (
    [ID] uniqueidentifier NOT NULL,
    [NOME] nvarchar(40) NOT NULL,
    [DESCRICAO] nvarchar(200) NULL,
    [DT_CRIACAO] datetime2 NOT NULL,
    CONSTRAINT [PK_ETIQUETA] PRIMARY KEY ([ID])
);
GO


CREATE TABLE [IDEMPOTENCY_KEY] (
    [CHAVE] nvarchar(100) NOT NULL,
    [ID_USUARIO] nvarchar(64) NOT NULL,
    [ENDPOINT] nvarchar(200) NOT NULL,
    [HASH_CORPO] nvarchar(64) NOT NULL,
    [STATUS_RESPOSTA] int NOT NULL,
    [CORPO_RESPOSTA] nvarchar(max) NULL,
    [TIPO_CONTEUDO] nvarchar(100) NULL,
    [LOCALIZACAO] nvarchar(300) NULL,
    [DT_CRIACAO] datetime2 NOT NULL,
    CONSTRAINT [PK_IDEMPOTENCY_KEY] PRIMARY KEY ([CHAVE])
);
GO


CREATE TABLE [COMENTARIO_ETIQUETA] (
    [ID_COMENTARIO] uniqueidentifier NOT NULL,
    [ID_ETIQUETA] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_COMENTARIO_ETIQUETA] PRIMARY KEY ([ID_COMENTARIO], [ID_ETIQUETA]),
    CONSTRAINT [FK_COMENT_ETIQ_COMENTARIO] FOREIGN KEY ([ID_COMENTARIO]) REFERENCES [COMENTARIO] ([ID]) ON DELETE CASCADE,
    CONSTRAINT [FK_COMENT_ETIQ_ETIQUETA] FOREIGN KEY ([ID_ETIQUETA]) REFERENCES [ETIQUETA] ([ID]) ON DELETE CASCADE
);
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ID', N'DT_CRIACAO', N'DESCRICAO', N'NOME') AND [object_id] = OBJECT_ID(N'[ETIQUETA]'))
    SET IDENTITY_INSERT [ETIQUETA] ON;
INSERT INTO [ETIQUETA] ([ID], [DT_CRIACAO], [DESCRICAO], [NOME])
VALUES ('8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01', '2026-11-10T00:00:00.0000000Z', N'O problema oferece risco imediato a quem passa pelo local.', N'Risco a pedestres'),
('8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e02', '2026-11-10T00:00:00.0000000Z', N'O problema já foi resolvido antes e voltou a acontecer.', N'Problema recorrente'),
('8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e03', '2026-11-10T00:00:00.0000000Z', N'Traz um dado novo para a equipe da zeladoria.', N'Informação nova'),
('8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e04', '2026-11-10T00:00:00.0000000Z', N'A situação piorou desde o report original.', N'Piorou');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'ID', N'DT_CRIACAO', N'DESCRICAO', N'NOME') AND [object_id] = OBJECT_ID(N'[ETIQUETA]'))
    SET IDENTITY_INSERT [ETIQUETA] OFF;
GO


CREATE UNIQUE INDEX [UX_APOIO_OCORRENCIA_USUARIO] ON [APOIO] ([ID_OCORRENCIA], [ID_USUARIO]);
GO


CREATE INDEX [IX_COMENTARIO_OCORRENCIA_DATA] ON [COMENTARIO] ([ID_OCORRENCIA], [DT_CRIACAO]);
GO


CREATE INDEX [IX_COMENTARIO_ETIQUETA_ID_ETIQUETA] ON [COMENTARIO_ETIQUETA] ([ID_ETIQUETA]);
GO


CREATE UNIQUE INDEX [UX_ETIQUETA_NOME] ON [ETIQUETA] ([NOME]);
GO


