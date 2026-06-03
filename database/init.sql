-- Создайте БД: CREATE DATABASE stroymaterialy ENCODING 'UTF8';
-- Затем выполните этот скрипт в базе stroymaterialy

CREATE TABLE IF NOT EXISTS roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(80) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    role_id INT NOT NULL REFERENCES roles(id),
    full_name VARCHAR(200) NOT NULL,
    login VARCHAR(120) NOT NULL UNIQUE,
    password VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id SERIAL PRIMARY KEY,
    article VARCHAR(20) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    price NUMERIC(12, 2) NOT NULL,
    supplier VARCHAR(120),
    manufacturer VARCHAR(120),
    category VARCHAR(120),
    discount_percent INT NOT NULL DEFAULT 0,
    quantity_in_stock INT NOT NULL DEFAULT 0,
    description TEXT,
    image_file VARCHAR(120)
);

CREATE TABLE IF NOT EXISTS pickup_points (
    id SERIAL PRIMARY KEY,
    address TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS orders (
    id SERIAL PRIMARY KEY,
    order_number INT NOT NULL UNIQUE,
    order_date DATE NOT NULL,
    delivery_date DATE NOT NULL,
    pickup_point_id INT REFERENCES pickup_points(id),
    client_full_name VARCHAR(200) NOT NULL,
    pickup_code VARCHAR(20),
    status VARCHAR(80) NOT NULL
);

CREATE TABLE IF NOT EXISTS order_items (
    id SERIAL PRIMARY KEY,
    order_id INT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id INT NOT NULL REFERENCES products(id),
    quantity INT NOT NULL
);

INSERT INTO roles (name) VALUES
    ('Администратор'),
    ('Менеджер'),
    ('Авторизованный клиент')
ON CONFLICT (name) DO NOTHING;
