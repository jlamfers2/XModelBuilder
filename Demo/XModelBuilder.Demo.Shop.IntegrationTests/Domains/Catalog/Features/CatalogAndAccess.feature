#language: en
Feature: Catalog and access
    Only an admin may extend the catalog,
    and a customer may only view their own orders.

Scenario: Admin adds a product
    Given I am logged in as admin "admin@shop.test"
    When I add the following product:
        | Field         | Value        |
        | Sku           | SKU-LAPTOP-9 |
        | Name          | Demo Laptop  |
        | UnitPrice     | 999.99       |
        | StockQuantity | 5            |
        | Category      | Laptops      |
    Then the product is added
    And the catalog contains a product with sku "SKU-LAPTOP-9"

Scenario: A customer may not add a product
    Given I am logged in as customer "alice@shop.test"
    When I add the following product:
        | Field         | Value      |
        | Sku           | SKU-HACK-1 |
        | Name          | Forbidden  |
        | UnitPrice     | 1.00       |
        | StockQuantity | 1          |
        | Category      | Books      |
    Then I am rejected as forbidden

Scenario: A customer does not see another customer's orders
    Given I am logged in as customer "alice@shop.test"
    And I have placed an order for 1 x "SKU-PHONE-1"
    When I am logged in as customer "bob@shop.test"
    And I request the orders of "alice@shop.test"
    Then I am rejected as forbidden
