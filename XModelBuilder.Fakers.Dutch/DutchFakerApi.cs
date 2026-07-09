using System.Text;

namespace XModelBuilder.Fakers.Dutch;

/// <summary>
/// The Dutch faker method surface: deterministic generators for Netherlands-specific identifiers and
/// contact data - BSN, RSIN, BTW, KvK, postcode, kenteken, phone numbers, and more. Wherever an
/// identifier carries an official check (the "elfproef" for BSN/RSIN/bank accounts, the ISO&#160;13616
/// mod-97 check for IBAN), the generated value is a VALID one for that check, so it is accepted by
/// systems that validate the structure. Values are fictitious and must only be used as test data.
///
/// <para>
/// This is the object exposed by the <see cref="DutchFaker"/> namespace member
/// <see cref="DutchFaker.NL"/>, so its methods are addressed as <c>nl.&lt;method&gt;()</c> from tokens
/// (e.g. <c>"nl.bsn()"</c>, <c>"nl.postcode()"</c>) and via <c>xprovider.NL()</c> from C#.
/// </para>
///
/// <para>
/// All randomness flows through the injected, seeded <see cref="Random"/>: build a fresh
/// ServiceProvider per test (each with the same seed) and every run reproduces. Separate providers get
/// separate seeded RNGs, so parallel tests stay isolated. The values depend on how many times the RNG
/// has been drawn before (call order), exactly like the RNG-based methods on XFaker.
/// </para>
/// </summary>
public class DutchFakerApi(Random random)
{
    // Letters allowed on modern Dutch number plates: vowels (A E I O U) are excluded so plates never
    // spell words, and C, Q, W, Y are avoided as well (never issued in the common sidecodes).
    private const string PlateLetters = "BDFGHJKLMNPRSTVXZ";

    // Realistic IBAN bank codes (the 4-letter segment after the check digits).
    private static readonly string[] BankCodes =
        ["INGB", "RABO", "ABNA", "TRIO", "SNSB", "ASNB", "KNAB", "BUNQ", "RBRB", "FVLB"];

    // Landline area codes paired with the length of the subscriber number, so area + subscriber is
    // always 10 digits (3-digit area -> 7-digit subscriber, 4-digit area -> 6-digit subscriber).
    private static readonly (string Area, int SubscriberLength)[] AreaCodes =
    [
        ("010", 7), ("013", 7), ("015", 7), ("020", 7), ("023", 7), ("024", 7), ("026", 7),
        ("030", 7), ("033", 7), ("035", 7), ("036", 7), ("038", 7), ("040", 7), ("043", 7),
        ("045", 7), ("050", 7), ("053", 7), ("055", 7), ("058", 7), ("070", 7), ("071", 7),
        ("072", 7), ("073", 7), ("076", 7), ("078", 7), ("079", 7),
        ("0111", 6), ("0113", 6), ("0118", 6), ("0161", 6), ("0180", 6), ("0182", 6), ("0222", 6),
        ("0229", 6), ("0251", 6), ("0299", 6), ("0343", 6), ("0475", 6), ("0512", 6), ("0592", 6),
    ];

    private static readonly string[] Provincies =
    [
        "Groningen", "Friesland", "Drenthe", "Overijssel", "Flevoland", "Gelderland", "Utrecht",
        "Noord-Holland", "Zuid-Holland", "Zeeland", "Noord-Brabant", "Limburg",
    ];

    // All current Dutch municipalities (gemeenten), per the official CBS/Wikipedia list (peildatum
    // 1 January 2024; unchanged for 2025). The two municipalities both displayed as "Bergen"
    // (Noord-Holland and Limburg) are listed once. The Caribbean public bodies (Bonaire, Saba, Sint
    // Eustatius) are not gemeenten and are excluded. Non-ASCII letters use \u escapes so the source
    // compiles regardless of file encoding.
    private static readonly string[] Gemeenten =
    [
        "'s-Hertogenbosch", "Aa en Hunze", "Aalsmeer", "Aalten", "Achtkarspelen", "Alblasserdam",
        "Albrandswaard", "Alkmaar", "Almelo", "Almere", "Alphen aan den Rijn", "Alphen-Chaam",
        "Altena", "Ameland", "Amersfoort", "Amstelveen", "Amsterdam", "Apeldoorn",
        "Arnhem", "Assen", "Asten", "Baarle-Nassau", "Baarn", "Barendrecht",
        "Barneveld", "Beek", "Beekdaelen", "Beesel", "Berg en Dal", "Bergeijk",
        "Bergen", "Bergen op Zoom", "Berkelland", "Bernheze", "Best", "Beuningen",
        "Beverwijk", "Bladel", "Blaricum", "Bloemendaal", "Bodegraven-Reeuwijk", "Boekel",
        "Borger-Odoorn", "Borne", "Borsele", "Boxtel", "Breda", "Bronckhorst",
        "Brummen", "Brunssum", "Bunnik", "Bunschoten", "Buren", "Capelle aan den IJssel",
        "Castricum", "Coevorden", "Cranendonck", "Culemborg", "Dalfsen", "Dantumadeel",
        "De Bilt", "De Friese Meren", "De Ronde Venen", "De Wolden", "Delft", "Den Haag",
        "Den Helder", "Deurne", "Deventer", "Diemen", "Dijk en Waard", "Dinkelland",
        "Doesburg", "Doetinchem", "Dongen", "Dordrecht", "Drechterland", "Drimmelen",
        "Dronten", "Druten", "Duiven", "Echt-Susteren", "Edam-Volendam", "Ede",
        "Eemnes", "Eemsdelta", "Eersel", "Eijsden-Margraten", "Eindhoven", "Elburg",
        "Emmen", "Enkhuizen", "Enschede", "Epe", "Ermelo", "Etten-Leur",
        "Geertruidenberg", "Geldrop-Mierlo", "Gemert-Bakel", "Gennep", "Gilze en Rijen", "Goeree-Overflakkee",
        "Goes", "Goirle", "Gooise Meren", "Gorinchem", "Gouda", "Groningen",
        "Gulpen-Wittem", "Haaksbergen", "Haarlem", "Haarlemmermeer", "Halderberge", "Hardenberg",
        "Harderwijk", "Hardinxveld-Giessendam", "Harlingen", "Hattem", "Heemskerk", "Heemstede",
        "Heerde", "Heerenveen", "Heerlen", "Heeze-Leende", "Heiloo", "Hellendoorn",
        "Helmond", "Hendrik-Ido-Ambacht", "Hengelo", "Het Hogeland", "Heumen", "Heusden",
        "Hillegom", "Hilvarenbeek", "Hilversum", "Hoeksche Waard", "Hof van Twente", "Hollands Kroon",
        "Hoogeveen", "Hoorn", "Horst aan de Maas", "Houten", "Huizen", "Hulst",
        "IJsselstein", "Kaag en Braassem", "Kampen", "Kapelle", "Katwijk", "Kerkrade",
        "Koggenland", "Krimpen aan den IJssel", "Krimpenerwaard", "Laarbeek", "Land van Cuijk", "Landgraaf",
        "Landsmeer", "Lansingerland", "Laren", "Leeuwarden", "Leiden", "Leiderdorp",
        "Leidschendam-Voorburg", "Lelystad", "Leudal", "Leusden", "Lingewaard", "Lisse",
        "Lochem", "Loon op Zand", "Lopik", "Losser", "Maasdriel", "Maasgouw",
        "Maashorst", "Maassluis", "Maastricht", "Medemblik", "Meerssen", "Meierijstad",
        "Meppel", "Middelburg", "Midden-Delfland", "Midden-Drenthe", "Midden-Groningen", "Moerdijk",
        "Molenlanden", "Montferland", "Montfoort", "Mook en Middelaar", "Neder-Betuwe", "Nederweert",
        "Nieuwegein", "Nieuwkoop", "Nijkerk", "Nijmegen", "Nissewaard", "Noardeast-Fryslân",
        "Noord-Beveland", "Noordenveld", "Noordoostpolder", "Noordwijk", "Nuenen c.a.", "Nunspeet",
        "Oegstgeest", "Oirschot", "Oisterwijk", "Oldambt", "Oldebroek", "Oldenzaal",
        "Olst-Wijhe", "Ommen", "Oost Gelre", "Oosterhout", "Ooststellingwerf", "Oostzaan",
        "Opmeer", "Opsterland", "Oss", "Oude IJsselstreek", "Ouder-Amstel", "Oudewater",
        "Overbetuwe", "Papendrecht", "Peel en Maas", "Pekela", "Pijnacker-Nootdorp", "Purmerend",
        "Putten", "Raalte", "Reimerswaal", "Renkum", "Renswoude", "Reusel-De Mierden",
        "Rheden", "Rhenen", "Ridderkerk", "Rijssen-Holten", "Rijswijk", "Roerdalen",
        "Roermond", "Roosendaal", "Rotterdam", "Rozendaal", "Rucphen", "Schagen",
        "Scherpenzeel", "Schiedam", "Schiermonnikoog", "Schouwen-Duiveland", "Simpelveld", "Sint-Michielsgestel",
        "Sittard-Geleen", "Sliedrecht", "Sluis", "Smallingerland", "Soest", "Someren",
        "Son en Breugel", "Stadskanaal", "Staphorst", "Stede Broec", "Steenbergen", "Steenwijkerland",
        "Stein", "Stichtse Vecht", "Súdwest-Fryslân", "Terneuzen", "Terschelling", "Texel",
        "Teylingen", "Tholen", "Tiel", "Tietjerksteradeel", "Tilburg", "Tubbergen",
        "Twenterand", "Tynaarlo", "Uitgeest", "Uithoorn", "Urk", "Utrecht",
        "Utrechtse Heuvelrug", "Vaals", "Valkenburg aan de Geul", "Valkenswaard", "Veendam", "Veenendaal",
        "Veere", "Veldhoven", "Velsen", "Venlo", "Venray", "Vijfheerenlanden",
        "Vlaardingen", "Vlieland", "Vlissingen", "Voerendaal", "Voorne aan Zee", "Voorschoten",
        "Voorst", "Vught", "Waadhoeke", "Waalre", "Waalwijk", "Waddinxveen",
        "Wageningen", "Wassenaar", "Waterland", "Weert", "West Betuwe", "West Maas en Waal",
        "Westerkwartier", "Westerveld", "Westervoort", "Westerwolde", "Westland", "Weststellingwerf",
        "Wierden", "Wijchen", "Wijdemeren", "Wijk bij Duurstede", "Winterswijk", "Woensdrecht",
        "Woerden", "Wormerland", "Woudenberg", "Zaanstad", "Zaltbommel", "Zandvoort",
        "Zeewolde", "Zeist", "Zevenaar", "Zoetermeer", "Zoeterwoude", "Zuidplas",
        "Zundert", "Zutphen", "Zwartewaterland", "Zwijndrecht", "Zwolle",
    ];

    // --- Identity / fiscal ---

    /// <summary>
    /// A valid Burgerservicenummer (BSN): 9 digits that satisfy the official "elfproef" (11-test).
    /// Fictitious - only for use as test data.
    /// </summary>
    /// <returns>A 9-digit BSN string that passes the 11-test.</returns>
    public string Bsn() => ElfproefNumberBsn();

    /// <summary>
    /// A valid RSIN (Rechtspersonen en Samenwerkingsverbanden Informatienummer): 9 digits that satisfy
    /// the same "elfproef" as the BSN.
    /// </summary>
    /// <returns>A 9-digit RSIN string that passes the 11-test.</returns>
    public string Rsin() => ElfproefNumberBsn();

    /// <summary>
    /// A BTW-nummer (VAT number) in the legal-entity format: <c>NL</c> + a 9-digit number that passes
    /// the 11-test (as an RSIN does) + <c>B</c> + a 2-digit sub-number, e.g. <c>NL123456782B01</c>.
    /// (Sole-proprietor BTW-identificatienummers use a different, randomized scheme; this generator
    /// produces the classic rechtspersoon form.)
    /// </summary>
    /// <returns>A BTW-nummer string.</returns>
    public string BtwNummer() => $"NL{ElfproefNumberBsn()}B{random.Next(1, 100):00}";

    /// <summary>A KvK-nummer (Chamber of Commerce number): 8 digits. There is no public checksum.</summary>
    /// <returns>An 8-digit KvK-nummer string.</returns>
    public string KvkNummer() => $"{random.Next(1, 10)}{Digits(7)}";

    /// <summary>A vestigingsnummer (KvK establishment number): 12 digits. There is no public checksum.</summary>
    /// <returns>A 12-digit vestigingsnummer string.</returns>
    public string Vestigingsnummer() => $"{random.Next(1, 10)}{Digits(11)}";

    /// <summary>An AGB-code (healthcare provider code): 8 digits. There is no public checksum.</summary>
    /// <returns>An 8-digit AGB-code string.</returns>
    public string AgbCode() => Digits(8);

    // --- Bank ---

    /// <summary>
    /// A valid Dutch IBAN: <c>NL</c> + 2 mod-97 check digits (ISO&#160;13616) + a 4-letter bank code +
    /// a 10-digit account number, e.g. <c>NL91ABNA0417164300</c>. The check digits are computed so the
    /// IBAN passes mod-97 validation.
    /// </summary>
    /// <returns>An 18-character Dutch IBAN string.</returns>
    public string Iban()
    {
        var bank = BankCodes[random.Next(BankCodes.Length)];
        var account = Digits(10);
        var check = 98 - Mod97($"{bank}{account}NL00");
        return $"NL{check:00}{bank}{account}";
    }

    /// <summary>
    /// A classic (pre-IBAN) Dutch bank account number: 9 digits that satisfy the bank "elfproef"
    /// (the weighted 9..1 sum is divisible by 11).
    /// </summary>
    /// <returns>A 9-digit bank account number string that passes the bank 11-test.</returns>
    public string Bankrekeningnummer() => ElfproefNumberBank();

    // --- Address / contact ---

    /// <summary>
    /// A Dutch postcode in the format <c>1234 AB</c>: four digits (1000-9999) and two uppercase
    /// letters, avoiding the letter pairs PostNL does not issue (SS, SD, SA).
    /// </summary>
    /// <returns>A postcode string such as <c>"1234 AB"</c>.</returns>
    public string Postcode()
    {
        var digits = random.Next(1000, 10000);
        string letters;
        do
        {
            letters = $"{(char)('A' + random.Next(26))}{(char)('A' + random.Next(26))}";
        }
        while (letters is "SS" or "SD" or "SA");

        return $"{digits} {letters}";
    }

    /// <summary>
    /// A Dutch number plate ("kenteken") in one of the common modern sidecodes (e.g. <c>XX-999-X</c>),
    /// using only the letters that are actually issued (no vowels, no C/Q/W/Y).
    /// </summary>
    /// <returns>A kenteken string such as <c>"GK-123-D"</c>.</returns>
    public string Kenteken()
    {
        // A handful of real modern sidecodes; 'L' = allowed letter, 'D' = digit.
        string[] patterns = ["LLL-DD-L", "LL-DDD-L", "L-DDD-LL", "DD-LLL-D", "D-LLL-DD", "DD-LL-DD"];
        var pattern = patterns[random.Next(patterns.Length)];
        var sb = new StringBuilder(pattern.Length);
        foreach (var c in pattern)
        {
            sb.Append(c switch
            {
                'L' => PlateLetters[random.Next(PlateLetters.Length)],
                'D' => (char)('0' + random.Next(10)),
                _ => c,
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// A Dutch mobile phone number in national format without separators: <c>06</c> followed by 8
    /// digits, e.g. <c>0612345678</c>.
    /// </summary>
    /// <returns>A 10-digit mobile number string starting with <c>06</c>.</returns>
    public string Mobiel() => $"06{Digits(8)}";

    /// <summary>
    /// A Dutch landline number in national format without separators: a real area code followed by a
    /// subscriber number, always 10 digits in total, e.g. <c>0201234567</c>.
    /// </summary>
    /// <returns>A 10-digit landline number string.</returns>
    public string VastTelefoonnummer()
    {
        var (area, subscriberLength) = AreaCodes[random.Next(AreaCodes.Length)];
        return $"{area}{Digits(subscriberLength)}";
    }

    /// <summary>
    /// A plausible Dutch passport/ID document number: 2 uppercase letters followed by 7 digits, e.g.
    /// <c>NX1234567</c>. There is no public checksum, so only the shape is realistic.
    /// </summary>
    /// <returns>A 9-character document number string.</returns>
    public string Paspoortnummer() =>
        $"{(char)('A' + random.Next(26))}{(char)('A' + random.Next(26))}{Digits(7)}";

    /// <summary>A Dutch driving-licence number: 10 digits. There is no public checksum.</summary>
    /// <returns>A 10-digit driving-licence number string.</returns>
    public string Rijbewijsnummer() => Digits(10);

    /// <summary>The name of one of the twelve Dutch provinces.</summary>
    /// <returns>A province name such as <c>"Utrecht"</c>.</returns>
    public string Provincie() => Provincies[random.Next(Provincies.Length)];

    /// <summary>The name of a Dutch municipality (gemeente).</summary>
    /// <returns>A municipality name such as <c>"Amsterdam"</c>.</returns>
    public string Gemeente() => Gemeenten[random.Next(Gemeenten.Length)];

    // --- Helpers ---

    // A string of exactly 'count' random decimal digits (leading zeros allowed).
    private string Digits(int count)
    {
        var sb = new StringBuilder(count);
        for (var i = 0; i < count; i++)
        {
            sb.Append((char)('0' + random.Next(10)));
        }

        return sb.ToString();
    }

    // 9 digits passing the BSN/RSIN "elfproef": 9*d1+8*d2+...+2*d8-1*d9 is divisible by 11.
    private string ElfproefNumberBsn()
    {
        while (true)
        {
            Span<int> d = stackalloc int[9];
            d[0] = random.Next(1, 10); // no leading zero
            var sum = 9 * d[0];
            for (var i = 1; i < 8; i++)
            {
                d[i] = random.Next(0, 10);
                sum += (9 - i) * d[i];
            }

            var check = sum % 11; // weight of d9 is -1, so d9 must equal sum mod 11
            if (check == 10)
            {
                continue; // no single-digit check possible; try again
            }

            d[8] = check;
            return DigitsToString(d);
        }
    }

    // 9 digits passing the bank "elfproef": 9*d1+8*d2+...+1*d9 is divisible by 11.
    private string ElfproefNumberBank()
    {
        while (true)
        {
            Span<int> d = stackalloc int[9];
            d[0] = random.Next(1, 10);
            var sum = 9 * d[0];
            for (var i = 1; i < 8; i++)
            {
                d[i] = random.Next(0, 10);
                sum += (9 - i) * d[i];
            }

            var check = (11 - (sum % 11)) % 11; // weight of d9 is 1
            if (check == 10)
            {
                continue;
            }

            d[8] = check;
            return DigitsToString(d);
        }
    }

    private static string DigitsToString(ReadOnlySpan<int> digits)
    {
        var sb = new StringBuilder(digits.Length);
        foreach (var digit in digits)
        {
            sb.Append((char)('0' + digit));
        }

        return sb.ToString();
    }

    // ISO 13616 mod-97 over a string where letters are expanded to A=10..Z=35, computed iteratively so
    // no big-integer type is needed.
    private static int Mod97(string value)
    {
        var remainder = 0;
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                var n = c - 'A' + 10; // two decimal digits
                remainder = (remainder * 10 + n / 10) % 97;
                remainder = (remainder * 10 + n % 10) % 97;
            }
        }

        return remainder;
    }
}
