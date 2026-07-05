#language: en
Feature: Fulfill order
    As a warehouse operator I want to ship paid orders,
    and unpaid orders or customers must not be able to.

Scenario: Warehouse ships a paid order
    Given I am logged in as customer "alice@shop.test"
    And I have placed a paid order for 1 x "SKU-PHONE-1"
    When I am logged in as warehouse operator "wendy@shop.test"
    And I ship the order
    Then the shipment is accepted
    And the order has status "Shipped"

Scenario: An unpaid order cannot be shipped
    Given I am logged in as customer "alice@shop.test"
    And I have placed an order for 1 x "SKU-PHONE-1"
    When I am logged in as warehouse operator "wendy@shop.test"
    And I ship the order
    Then the shipment is rejected

Scenario: A customer may not ship orders
    Given I am logged in as customer "alice@shop.test"
    And I have placed a paid order for 1 x "SKU-PHONE-1"
    When I ship the order
    Then I am rejected as forbidden
