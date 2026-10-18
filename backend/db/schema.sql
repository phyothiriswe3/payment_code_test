CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `customer` (
    `id` int NOT NULL AUTO_INCREMENT,
    `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `email` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_customer` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `product` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `price` decimal(12,2) NOT NULL,
    `currency` char(3) CHARACTER SET utf8mb4 NOT NULL,
    `payment_type` enum('OneTime','Recurring') CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_product` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `customer_card` (
    `id` int NOT NULL AUTO_INCREMENT,
    `customer_id` int NOT NULL,
    `customer_token` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `masked_card_no` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_customer_card` PRIMARY KEY (`id`),
    CONSTRAINT `fk_customer_card_customer` FOREIGN KEY (`customer_id`) REFERENCES `customer` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `transaction` (
    `id` bigint NOT NULL AUTO_INCREMENT,
    `customer_id` int NOT NULL,
    `product_id` int NOT NULL,
    `customer_card_id` int NULL,
    `invoice_no` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `idempotency_key` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `request_fingerprint` char(64) CHARACTER SET utf8mb4 NOT NULL,
    `amount` decimal(12,2) NOT NULL,
    `currency` char(3) CHARACTER SET utf8mb4 NOT NULL,
    `status` enum('Pending','Succeeded','Failed') CHARACTER SET utf8mb4 NOT NULL,
    `flow` enum('HostedPage','SavedCard') CHARACTER SET utf8mb4 NOT NULL,
    `payment_token` varchar(512) CHARACTER SET utf8mb4 NULL,
    `redirect_url` varchar(2048) CHARACTER SET utf8mb4 NULL,
    `payment_channel` varchar(20) CHARACTER SET utf8mb4 NULL,
    `masked_card_no` varchar(30) CHARACTER SET utf8mb4 NULL,
    `provider_ref` varchar(50) CHARACTER SET utf8mb4 NULL,
    `resp_code` varchar(10) CHARACTER SET utf8mb4 NULL,
    `resp_desc` varchar(100) CHARACTER SET utf8mb4 NULL,
    `created_at` datetime(6) NOT NULL,
    `paid_at` datetime(6) NULL,
    `updated_at` datetime(6) NOT NULL,
    `processed_at` datetime(6) NULL,
    CONSTRAINT `PK_transaction` PRIMARY KEY (`id`),
    CONSTRAINT `fk_transaction_customer` FOREIGN KEY (`customer_id`) REFERENCES `customer` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transaction_customer_card` FOREIGN KEY (`customer_card_id`) REFERENCES `customer_card` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_transaction_product` FOREIGN KEY (`product_id`) REFERENCES `product` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

INSERT INTO `customer` (`id`, `email`, `first_name`, `last_name`)
VALUES (1, 'aung.min@example.com', 'Aung', 'Min'),
(2, 'meiling.tan@example.com', 'Mei Ling', 'Tan'),
(3, 'rajesh.kumar@example.com', 'Rajesh', 'Kumar');

INSERT INTO `product` (`id`, `currency`, `description`, `name`, `payment_type`, `price`)
VALUES (1, 'SGD', 'Single-day gym access.', 'Day Pass', 'OneTime', 25.0),
(2, 'SGD', 'One group fitness class of your choice.', 'Class Drop-in', 'OneTime', 18.5),
(3, 'SGD', 'Unlimited access, billed monthly to a saved card.', 'Monthly Membership', 'Recurring', 99.0),
(4, 'SGD', 'Unlimited access for 12 months, renewed with a saved card.', 'Annual Membership', 'Recurring', 899.0);

CREATE UNIQUE INDEX `ux_customer_email` ON `customer` (`email`);

CREATE UNIQUE INDEX `ux_customer_card_customer_token` ON `customer_card` (`customer_id`, `customer_token`);

CREATE INDEX `ix_transaction_customer_card_id` ON `transaction` (`customer_card_id`);

CREATE INDEX `ix_transaction_customer_id` ON `transaction` (`customer_id`);

CREATE INDEX `ix_transaction_product_id` ON `transaction` (`product_id`);

CREATE INDEX `ix_transaction_status` ON `transaction` (`status`);

CREATE UNIQUE INDEX `ux_transaction_idempotency_key` ON `transaction` (`idempotency_key`);

CREATE UNIQUE INDEX `ux_transaction_invoice_no` ON `transaction` (`invoice_no`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261017144535_Initial', '8.0.11');

COMMIT;

