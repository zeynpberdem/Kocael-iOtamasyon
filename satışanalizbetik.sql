USE [per10Database]
GO

/****** Object:  Table [dbo].[SatislarAnaliz]    Script Date: 22.05.2026 15:01:38 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[SatislarAnaliz](
	[SatisID] [int] IDENTITY(1,1) NOT NULL,
	[UrunID] [int] NOT NULL,
	[SepetID] [int] NOT NULL,
	[SatisTarihi] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[SatisID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[SatislarAnaliz] ADD  DEFAULT (getdate()) FOR [SatisTarihi]
GO

ALTER TABLE [dbo].[SatislarAnaliz]  WITH CHECK ADD  CONSTRAINT [FK_SatislarAnaliz_Urunler] FOREIGN KEY([UrunID])
REFERENCES [dbo].[Urunler] ([UrunID])
GO

ALTER TABLE [dbo].[SatislarAnaliz] CHECK CONSTRAINT [FK_SatislarAnaliz_Urunler]
GO

