CREATE TABLE Products (
    -- Using NVARCHAR(450) to make it a good key, but you could use UNIQUEIDENTIFIER
    ProductId NVARCHAR(450) PRIMARY KEY, 
    
    ProductName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    
    -- Use DECIMAL for money in SQL
    Price DECIMAL(18, 2) NOT NULL, 
    
    StockAvailable INT NOT NULL,
    ImageUrl NVARCHAR(MAX), -- For long URLs
    IsActive BIT NOT NULL DEFAULT 1,
    DateAdded DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);