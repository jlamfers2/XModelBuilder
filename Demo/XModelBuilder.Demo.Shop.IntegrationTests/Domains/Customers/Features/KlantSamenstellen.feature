#language: nl
Functionaliteit: Klant stapsgewijs samenstellen
    Als testauteur wil ik een klant geaggregeerd opbouwen - eerst als persoon en
    daarna in aparte stappen uitbreiden met adressen - zodat elke Gherkin-regel maar
    over één ding gaat en XModelBuilder de rest (o.a. Bogus-data) invult.

Scenario: Een klant wordt als persoon gebouwd en daarna uitgebreid met adressen
    Gegeven ik bouw een klant op als persoon:
        | Veld     | Waarde          |
        | FullName | Carla Consument |
    En ik de klant uitbreid met een verzendadres:
        | Veld        | Waarde     |
        | Street      | Kerkstraat |
        | HouseNumber | 12         |
        | PostalCode  | 3511 AA    |
        | City        | Utrecht    |
    En ik de klant uitbreid met een factuuradres:
        | Veld        | Waarde     |
        | Street      | Dorpsplein |
        | HouseNumber | 5          |
        | PostalCode  | 1000 AA    |
        | City        | Amsterdam  |
    Dan heet de klant "Carla Consument"
    En heeft de klant een gegenereerd e-mailadres
    En heeft de klant 2 adressen
    En heeft de klant een verzendadres in "Utrecht"
    En heeft de klant een factuuradres in "Amsterdam"

Scenario: Een klant kan als persoon met alleen een verzendadres worden gebouwd
    Gegeven ik bouw een klant op als persoon:
        | Veld     | Waarde      |
        | FullName | Piet Prospect |
    En ik de klant uitbreid met een verzendadres:
        | Veld   | Waarde    |
        | City   | Rotterdam |
    Dan heeft de klant 1 adres
    En heeft de klant een verzendadres in "Rotterdam"
