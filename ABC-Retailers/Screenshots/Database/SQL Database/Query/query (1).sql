CREATE TABLE Users (
    UserId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Username NVARCHAR(50) UNIQUE NOT NULL,
    Email NVARCHAR(100) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Salt NVARCHAR(255) NOT NULL,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    PhoneNumber NVARCHAR(20),
    Address NVARCHAR(200),
    Role NVARCHAR(20) NOT NULL DEFAULT 'Customer', -- 'Admin' or 'Customer'
    IsActive BIT NOT NULL DEFAULT 1,
    DateRegistered DATETIME NOT NULL DEFAULT GETDATE(),
    LastLogin DATETIME,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

-- Indexes for faster queries
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Role ON Users(Role);

-- Orders table (Updated to support guest checkout)
CREATE TABLE Orders (
    OrderId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NULL, -- Nullable to support guest checkout
    OrderNumber NVARCHAR(50) UNIQUE NOT NULL,
    OrderDate DATETIME NOT NULL DEFAULT GETDATE(),
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', 
    -- Status options: Pending, Confirmed, Processing, Shipped, Delivered, Cancelled
    
    -- Customer Information (always required for both guest and registered users)
    CustomerName NVARCHAR(100) NOT NULL,
    CustomerEmail NVARCHAR(100) NOT NULL,
    ShippingAddress NVARCHAR(200) NOT NULL,
    PhoneNumber NVARCHAR(20),
    
    -- Financial Information
    Subtotal DECIMAL(18,2) NOT NULL,
    Tax DECIMAL(18,2) NOT NULL,
    ShippingCost DECIMAL(18,2) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    
    -- Tracking
    TrackingNumber NVARCHAR(100),
    ShippedDate DATETIME,
    DeliveredDate DATETIME,
    
    -- Additional Information
    OrderNotes NVARCHAR(500),
    
    -- Audit
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    
    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE SET NULL
);

-- Indexes
CREATE INDEX IX_Orders_UserId ON Orders(UserId);
CREATE INDEX IX_Orders_OrderNumber ON Orders(OrderNumber);
CREATE INDEX IX_Orders_Status ON Orders(Status);
CREATE INDEX IX_Orders_OrderDate ON Orders(OrderDate);
CREATE INDEX IX_Orders_CustomerEmail ON Orders(CustomerEmail);

-- Order items table for individual products in orders
CREATE TABLE OrderItems (
    OrderItemId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OrderId UNIQUEIDENTIFIER NOT NULL,
    ProductId NVARCHAR(100) NOT NULL, -- References Azure Table Storage RowKey
    ProductName NVARCHAR(200) NOT NULL,
    ProductImageUrl NVARCHAR(500),
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    TotalPrice DECIMAL(18,2) NOT NULL CHECK (TotalPrice >= 0),
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId) ON DELETE CASCADE
);

-- Indexes
CREATE INDEX IX_OrderItems_OrderId ON OrderItems(OrderId);
CREATE INDEX IX_OrderItems_ProductId ON OrderItems(ProductId);

-- ========================================
-- OPTIONAL: Cart Persistence Table
-- (Currently using session storage, but this allows for persistent carts)
-- ========================================

-- Uncomment if you want to persist carts in the database instead of session
/*
CREATE TABLE Carts (
    CartId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NULL, -- NULL for guest carts
    SessionId NVARCHAR(200), -- For guest users
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    ExpiresAt DATETIME NOT NULL DEFAULT DATEADD(DAY, 7, GETDATE()),
    
    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE TABLE CartItems (
    CartItemId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CartId UNIQUEIDENTIFIER NOT NULL,
    ProductId NVARCHAR(100) NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    ProductImageUrl NVARCHAR(500),
    Quantity INT NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    TotalPrice DECIMAL(18,2) NOT NULL CHECK (TotalPrice >= 0),
    AddedAt DATETIME NOT NULL DEFAULT GETDATE(),
    
    FOREIGN KEY (CartId) REFERENCES Carts(CartId) ON DELETE CASCADE
);

CREATE INDEX IX_Carts_UserId ON Carts(UserId);
CREATE INDEX IX_Carts_SessionId ON Carts(SessionId);
CREATE INDEX IX_CartItems_CartId ON CartItems(CartId);
CREATE INDEX IX_CartItems_ProductId ON CartItems(ProductId);
*/

-- ========================================
-- Seed Data
-- ========================================

-- Create default admin user (password will be set via application)
INSERT INTO Users (UserId, Username, Email, PasswordHash, Salt, FirstName, LastName, Role, IsActive)
VALUES (
    NEWID(),
    'admin',
    'admin@abcretailers.com',
    'PLACEHOLDER_HASH', -- Update via application
    'PLACEHOLDER_SALT', -- Update via application
    'System',
    'Administrator',
    'Admin',
    1
);

-- Create test customer user (password will be set via application)
INSERT INTO Users (UserId, Username, Email, PasswordHash, Salt, FirstName, LastName, Role, IsActive)
VALUES (
    NEWID(),
    'customer',
    'customer@test.com',
    'PLACEHOLDER_HASH', -- Update via application
    'PLACEHOLDER_SALT', -- Update via application
    'Test',
    'Customer',
    'Customer',
    1
);