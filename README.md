# ST104452280-CLDV6212-POE-Part-3

ABCRetailers E-commerce Web Application
ABCRetailers is a complete e-commerce web application built with ASP.NET Core and C#. It demonstrates a full-featured online store, from user authentication and product management to a session-based shopping cart and a secure checkout process.

The project uses a hybrid data storage architecture, leveraging Azure SQL for transactional data (Users, Products, Orders) and Azure Storage (Blobs, Tables, Queues) for file storage and other data.

🚀 Key Features
Authentication: Secure user registration and login with email/password.

Authorization: Role-based access control (Admin vs. Customer). Admins can see all orders, while customers can only see their own.

My Profile: A dedicated page for users to view and update their personal information.

Product Management (SQL): Full CRUD (Create, Read, Update, Delete) for products.

Image Storage (Blob): Product images are uploaded to Azure Blob Storage, with the URL/filename stored in the SQL Products table.

Shopping Cart (Session): A session-based shopping cart with features to add, update quantity, remove items, and clear the cart.

Secure Checkout: A multi-step checkout process that validates stock, calculates totals (subtotal, tax, shipping), and creates a new order in the database.

Order Management (SQL): All orders are stored in the SQL database.

Hybrid Data:

Azure SQL: Manages Users, Products, Orders, and OrderItems.

Azure Blob Storage: Stores product images.

Azure Table Storage: (As configured) Stores Customer data.

Azure Queue Storage: (As configured) Used for sending order notifications.

🛠️ Technology Stack
Framework: ASP.NET Core (using .NET)

Language: C#

Database (Data): Azure SQL Database

Data Access: Dapper

Database (Files & NoSQL): Azure Storage

Blob Storage (for product images)

Table Storage (for Customer records)

Queue Storage (for notifications)

Authentication: ASP.NET Core Cookie Authentication

Frontend: Razor Views, Bootstrap 5, Font Awesome (for icons)

⚙️ Setup and Installation
To run this project locally, follow these steps:

1. Prerequisites
.NET 8 SDK (or newer)

Visual Studio 2022 or VS Code

An active Azure Subscription

An Azure SQL Server & Database

An Azure Storage Account
