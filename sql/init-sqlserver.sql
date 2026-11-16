-- ========================================================================
-- MYMO PROJECT - FULL SQL SERVER DATABASE SCHEMA & SEED DATA SCRIPT
-- ========================================================================

--------------------------------------------------------------------------
-- 1. DATABASE: mymo_identity
--------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'mymo_identity')
BEGIN
    CREATE DATABASE mymo_identity;
END
GO

USE mymo_identity;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserId UNIQUEIDENTIFIER PRIMARY KEY,
        Username NVARCHAR(450) NOT NULL UNIQUE,
        DisplayName NVARCHAR(MAX) NOT NULL,
        Email NVARCHAR(450) NOT NULL UNIQUE,
        PhoneNumber NVARCHAR(MAX) NULL,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        AvatarUrl NVARCHAR(MAX) NULL,
        CoverUrl NVARCHAR(MAX) NULL,
        Bio NVARCHAR(MAX) NULL,
        Gender NVARCHAR(MAX) NULL,
        DateOfBirth DATE NULL,
        Latitude FLOAT NULL,
        Longitude FLOAT NULL,
        IsOnline BIT NOT NULL DEFAULT 0,
        LastSeen DATETIME2 NULL,
        LastActiveAt DATETIME2 NULL,
        Role NVARCHAR(MAX) NOT NULL DEFAULT 'User',
        Status NVARCHAR(MAX) NOT NULL DEFAULT 'Active',
        IsLocationSharing BIT NOT NULL DEFAULT 1,
        IsAnonymous BIT NOT NULL DEFAULT 0,
        MapMarker NVARCHAR(MAX) NULL,
        AvatarFrame NVARCHAR(MAX) NULL,
        ProfileTheme NVARCHAR(MAX) NULL,
        ProfileTitle NVARCHAR(MAX) NULL,
        IsPremium BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0,
        FavoritePlacesJson NVARCHAR(MAX) NOT NULL DEFAULT '[]'
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sessions')
BEGIN
    CREATE TABLE Sessions (
        SessionId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        RefreshToken NVARCHAR(450) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL
    );
    CREATE INDEX IX_Sessions_RefreshToken ON Sessions(RefreshToken);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Otps')
BEGIN
    CREATE TABLE Otps (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        Email NVARCHAR(450) NOT NULL,
        Purpose NVARCHAR(450) NOT NULL DEFAULT 'register',
        Code NVARCHAR(MAX) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL
    );
    CREATE INDEX IX_Otps_Email_Purpose ON Otps(Email, Purpose);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reports')
BEGIN
    CREATE TABLE Reports (
        ReportId UNIQUEIDENTIFIER PRIMARY KEY,
        ReporterId UNIQUEIDENTIFIER NOT NULL,
        TargetType NVARCHAR(MAX) NOT NULL DEFAULT 'Post',
        TargetId UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(MAX) NOT NULL DEFAULT '',
        Status NVARCHAR(MAX) NOT NULL DEFAULT 'Pending',
        TargetSummary NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        AuditLogId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Action NVARCHAR(MAX) NOT NULL DEFAULT 'Update',
        EntityName NVARCHAR(MAX) NOT NULL DEFAULT '',
        EntityId UNIQUEIDENTIFIER NOT NULL,
        OldValue NVARCHAR(MAX) NULL,
        NewValue NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

--------------------------------------------------------------------------
-- 2. DATABASE: mymo_social
--------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'mymo_social')
BEGIN
    CREATE DATABASE mymo_social;
END
GO

USE mymo_social;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Posts')
BEGIN
    CREATE TABLE Posts (
        PostId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(MAX) NOT NULL DEFAULT '',
        Username NVARCHAR(MAX) NOT NULL DEFAULT '',
        AvatarUrl NVARCHAR(MAX) NULL,
        AuthorIsPremium BIT NOT NULL DEFAULT 0,
        Caption NVARCHAR(MAX) NULL,
        PostType NVARCHAR(MAX) NOT NULL DEFAULT 'Photo',
        Visibility NVARCHAR(MAX) NOT NULL DEFAULT 'Public',
        IsAnonymous BIT NOT NULL DEFAULT 0,
        AnonymousAlias NVARCHAR(MAX) NULL,
        PlaceId UNIQUEIDENTIFIER NULL,
        PlaceName NVARCHAR(MAX) NULL,
        PlaceAddress NVARCHAR(MAX) NULL,
        Latitude FLOAT NULL,
        Longitude FLOAT NULL,
        LikeCount INT NOT NULL DEFAULT 0,
        CommentCount INT NOT NULL DEFAULT 0,
        ViewCount INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ExpiresAt DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Media')
BEGIN
    CREATE TABLE Media (
        MediaId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        PostId UNIQUEIDENTIFIER NULL,
        Url NVARCHAR(MAX) NOT NULL DEFAULT '',
        ThumbnailUrl NVARCHAR(MAX) NULL,
        MediaType NVARCHAR(MAX) NOT NULL DEFAULT 'Image',
        Width INT NULL,
        Height INT NULL,
        Duration INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Comments')
BEGIN
    CREATE TABLE Comments (
        CommentId UNIQUEIDENTIFIER PRIMARY KEY,
        PostId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        ParentCommentId UNIQUEIDENTIFIER NULL,
        Content NVARCHAR(MAX) NOT NULL DEFAULT '',
        LikeCount INT NOT NULL DEFAULT 0,
        DisplayName NVARCHAR(MAX) NOT NULL DEFAULT '',
        Username NVARCHAR(MAX) NOT NULL DEFAULT '',
        AvatarUrl NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Likes')
BEGIN
    CREATE TABLE Likes (
        LikeId UNIQUEIDENTIFIER PRIMARY KEY,
        PostId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT UQ_Likes_Post_User UNIQUE (PostId, UserId)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Views')
BEGIN
    CREATE TABLE Views (
        PostViewId UNIQUEIDENTIFIER PRIMARY KEY,
        PostId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Friendships')
BEGIN
    CREATE TABLE Friendships (
        FriendshipId UNIQUEIDENTIFIER PRIMARY KEY,
        RequesterId UNIQUEIDENTIFIER NOT NULL,
        ReceiverId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(MAX) NOT NULL DEFAULT 'Pending',
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Friendships_Requester_Receiver ON Friendships(RequesterId, ReceiverId);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        NotificationId UNIQUEIDENTIFIER PRIMARY KEY,
        ReceiverId UNIQUEIDENTIFIER NOT NULL,
        SenderId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(MAX) NOT NULL DEFAULT 'info',
        Title NVARCHAR(MAX) NULL,
        Content NVARCHAR(MAX) NULL,
        ReferenceId UNIQUEIDENTIFIER NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        SenderName NVARCHAR(MAX) NULL,
        SenderAvatar NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

--------------------------------------------------------------------------
-- 3. DATABASE: mymo_map
--------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'mymo_map')
BEGIN
    CREATE DATABASE mymo_map;
END
GO

USE mymo_map;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        PlaceCategoryId UNIQUEIDENTIFIER PRIMARY KEY,
        Name NVARCHAR(MAX) NOT NULL DEFAULT '',
        Icon NVARCHAR(MAX) NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Places')
BEGIN
    CREATE TABLE Places (
        PlaceId UNIQUEIDENTIFIER PRIMARY KEY,
        BusinessId UNIQUEIDENTIFIER NULL,
        BusinessName NVARCHAR(MAX) NOT NULL DEFAULT '',
        LogoUrl NVARCHAR(MAX) NULL,
        Verified BIT NOT NULL DEFAULT 0,
        CategoryId UNIQUEIDENTIFIER NOT NULL,
        CategoryName NVARCHAR(MAX) NOT NULL DEFAULT '',
        Name NVARCHAR(MAX) NOT NULL DEFAULT '',
        Description NVARCHAR(MAX) NULL,
        Address NVARCHAR(MAX) NULL,
        City NVARCHAR(MAX) NULL,
        Phone NVARCHAR(MAX) NULL,
        OpeningHours NVARCHAR(MAX) NULL,
        Latitude FLOAT NOT NULL DEFAULT 0,
        Longitude FLOAT NOT NULL DEFAULT 0,
        ThumbnailUrl NVARCHAR(MAX) NULL,
        Vibes NVARCHAR(MAX) NOT NULL DEFAULT '',
        Status NVARCHAR(MAX) NOT NULL DEFAULT 'Approved',
        AverageRating FLOAT NOT NULL DEFAULT 0,
        ReviewCount INT NOT NULL DEFAULT 0,
        CheckInCount INT NOT NULL DEFAULT 0,
        AdPackageId NVARCHAR(MAX) NULL,
        OwnerId UNIQUEIDENTIFIER NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Photos')
BEGIN
    CREATE TABLE Photos (
        PlacePhotoId UNIQUEIDENTIFIER PRIMARY KEY,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        ImageUrl NVARCHAR(MAX) NOT NULL DEFAULT '',
        IsPrimary BIT NOT NULL DEFAULT 0,
        DisplayOrder INT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CheckIns')
BEGIN
    CREATE TABLE CheckIns (
        CheckInId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        Latitude FLOAT NOT NULL DEFAULT 0,
        Longitude FLOAT NOT NULL DEFAULT 0,
        Mood NVARCHAR(MAX) NULL,
        Weather NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reviews')
BEGIN
    CREATE TABLE Reviews (
        ReviewId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(MAX) NOT NULL DEFAULT '',
        AvatarUrl NVARCHAR(MAX) NULL,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        Rating INT NOT NULL DEFAULT 5,
        Title NVARCHAR(MAX) NULL,
        Content NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Impressions')
BEGIN
    CREATE TABLE Impressions (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

--------------------------------------------------------------------------
-- 4. DATABASE: mymo_chat
--------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'mymo_chat')
BEGIN
    CREATE DATABASE mymo_chat;
END
GO

USE mymo_chat;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Conversations')
BEGIN
    CREATE TABLE Conversations (
        ConversationId UNIQUEIDENTIFIER PRIMARY KEY,
        Type NVARCHAR(MAX) NOT NULL DEFAULT 'Private',
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        LastMessage NVARCHAR(MAX) NULL,
        LastMessageTime DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Members')
BEGIN
    CREATE TABLE Members (
        MemberId UNIQUEIDENTIFIER PRIMARY KEY,
        ConversationId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        LastReadAt DATETIME2 NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Messages')
BEGIN
    CREATE TABLE Messages (
        MessageId UNIQUEIDENTIFIER PRIMARY KEY,
        ConversationId UNIQUEIDENTIFIER NOT NULL,
        SenderId UNIQUEIDENTIFIER NOT NULL,
        SenderName NVARCHAR(MAX) NOT NULL DEFAULT '',
        SenderAvatar NVARCHAR(MAX) NULL,
        MessageType INT NOT NULL DEFAULT 0,
        Content NVARCHAR(MAX) NOT NULL DEFAULT '',
        MediaUrl NVARCHAR(MAX) NULL,
        Latitude FLOAT NULL,
        Longitude FLOAT NULL,
        ReferenceId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

--------------------------------------------------------------------------
-- 5. DATABASE: mymo_business
--------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'mymo_business')
BEGIN
    CREATE DATABASE mymo_business;
END
GO

USE mymo_business;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Businesses')
BEGIN
    CREATE TABLE Businesses (
        BusinessId UNIQUEIDENTIFIER PRIMARY KEY,
        OwnerId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(MAX) NOT NULL DEFAULT '',
        Description NVARCHAR(MAX) NULL,
        LogoUrl NVARCHAR(MAX) NULL,
        Phone NVARCHAR(MAX) NULL,
        Email NVARCHAR(MAX) NULL,
        Website NVARCHAR(MAX) NULL,
        Address NVARCHAR(MAX) NULL,
        Latitude FLOAT NULL,
        Longitude FLOAT NULL,
        Vibes NVARCHAR(MAX) NOT NULL DEFAULT 'chill',
        Verified BIT NOT NULL DEFAULT 0,
        Status NVARCHAR(MAX) NOT NULL DEFAULT 'Approved',
        RejectReason NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Menus')
BEGIN
    CREATE TABLE Menus (
        MenuId UNIQUEIDENTIFIER PRIMARY KEY,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(MAX) NOT NULL DEFAULT 'Menu',
        Description NVARCHAR(MAX) NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MenuItems')
BEGIN
    CREATE TABLE MenuItems (
        MenuItemId UNIQUEIDENTIFIER PRIMARY KEY,
        MenuId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(MAX) NOT NULL DEFAULT '',
        Description NVARCHAR(MAX) NULL,
        Price DECIMAL(18,2) NOT NULL DEFAULT 0,
        ImageUrl NVARCHAR(MAX) NULL,
        CategoryName NVARCHAR(MAX) NULL,
        DisplayOrder INT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Promotions')
BEGIN
    CREATE TABLE Promotions (
        PromotionId UNIQUEIDENTIFIER PRIMARY KEY,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(MAX) NOT NULL DEFAULT '',
        Description NVARCHAR(MAX) NULL,
        ImageUrl NVARCHAR(MAX) NULL,
        StartAt DATETIME2 NULL,
        EndAt DATETIME2 NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Packages')
BEGIN
    CREATE TABLE Packages (
        ProductPackageId UNIQUEIDENTIFIER PRIMARY KEY,
        Segment NVARCHAR(MAX) NOT NULL DEFAULT 'B2C',
        Code NVARCHAR(MAX) NOT NULL DEFAULT '',
        DisplayName NVARCHAR(MAX) NOT NULL DEFAULT '',
        PriceVnd DECIMAL(18,2) NOT NULL DEFAULT 0,
        DurationDays INT NULL,
        BillingUnit NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        SortOrder INT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PremiumOrders')
BEGIN
    CREATE TABLE PremiumOrders (
        OrderId UNIQUEIDENTIFIER PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        PlanId NVARCHAR(MAX) NOT NULL DEFAULT 'monthly',
        AmountVnd DECIMAL(18,2) NOT NULL DEFAULT 0,
        PaymentStatus NVARCHAR(MAX) NOT NULL DEFAULT 'Pending',
        TransferReferenceCode NVARCHAR(MAX) NOT NULL DEFAULT '',
        PaymentSubmittedAt DATETIME2 NULL,
        PaidAt DATETIME2 NULL,
        ValidFrom DATETIME2 NULL,
        ValidUntil DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Campaigns')
BEGIN
    CREATE TABLE Campaigns (
        CampaignId UNIQUEIDENTIFIER PRIMARY KEY,
        BusinessId UNIQUEIDENTIFIER NOT NULL,
        PlaceId UNIQUEIDENTIFIER NOT NULL,
        PackageId NVARCHAR(MAX) NOT NULL DEFAULT '',
        AmountVnd DECIMAL(18,2) NOT NULL DEFAULT 0,
        PaymentStatus NVARCHAR(MAX) NOT NULL DEFAULT 'Pending',
        TransferReferenceCode NVARCHAR(MAX) NOT NULL DEFAULT '',
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO
