#language: en
Feature: Place order
    As a customer I want to place an order
    so that I can buy products from the catalog.

Scenario: Customer places a valid order
    Given I am logged in as customer "alice@shop.test"
    When I place the following order:
        | Field             | Value       |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 2           |
    Then the order is created
    And the order has status "Pending"
    And the total amount is 1000.00

Scenario: Order with insufficient stock is rejected
    Given I am logged in as customer "alice@shop.test"
    When I place the following order:
        | Field             | Value      |
        | Lines[0].Sku      | SKU-BOOK-1 |
        | Lines[0].Quantity | 5          |
    Then the order is rejected due to insufficient stock

Scenario: A guest may not place an order
    Given I am not logged in
    When I place the following order:
        | Field             | Value       |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 1           |
    Then I am rejected as unauthorized

Scenario: Order with a discount code lowers the total
    Given I am logged in as customer "alice@shop.test"
    When I place the following order:
        | Field             | Value       |
        | Lines[0].Sku      | SKU-PHONE-1 |
        | Lines[0].Quantity | 1           |
        | DiscountCode      | WELCOME10   |
    Then the order is created
    And the discount amount is 50.00
    And the total amount is 450.00
