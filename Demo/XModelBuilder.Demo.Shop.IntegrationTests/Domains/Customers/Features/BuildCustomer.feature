#language: en
Feature: Build a customer step by step
    As a test author I want to build a customer in aggregate - first as a person and
    then extend it in separate Gherkin steps with addresses - so that every line is about
    one thing and XModelBuilder fills in the rest (Bogus data among others).

Scenario: A customer is built as a person and then extended with addresses
    Given I start building a customer as a person:
        | Field    | Value           |
        | FullName | Carla Consumer  |
    And I extend the customer with a shipping address:
        | Field       | Value      |
        | Street      | Church St  |
        | HouseNumber | 12         |
        | PostalCode  | 3511 AA    |
        | City        | Utrecht    |
    And I extend the customer with a billing address:
        | Field       | Value      |
        | Street      | Town Sq    |
        | HouseNumber | 5          |
        | PostalCode  | 1000 AA    |
        | City        | Amsterdam  |
    Then the customer is named "Carla Consumer"
    And the customer has a generated email address
    And the customer has 2 addresses
    And the customer has a shipping address in "Utrecht"
    And the customer has a billing address in "Amsterdam"

Scenario: A customer can be built as a person with only a shipping address
    Given I start building a customer as a person:
        | Field    | Value        |
        | FullName | Pete Prospect |
    And I extend the customer with a shipping address:
        | Field | Value     |
        | City  | Rotterdam |
    Then the customer has 1 address
    And the customer has a shipping address in "Rotterdam"
