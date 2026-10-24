using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private int _settingsTabIndex;
    private string _timeZoneFilter = string.Empty;
    private bool _editingCustomUtc;
    private string _customUtcInput = string.Empty;
    private string _armedBetPresetName = string.Empty;
    private DateTime _armedBetPresetUntil = DateTime.MinValue;
    private string _editingBetPresetColor = string.Empty;

    private sealed class TimeZoneOption
    {
        public TimeZoneOption(int offsetMinutes, string name)
        {
            OffsetMinutes = offsetMinutes;
            Name = name;
        }

        public int OffsetMinutes { get; }
        public string Name { get; }
    }

    private static readonly TimeZoneOption[] TimeZoneOptions = BuildTimeZoneOptions();

    private static TimeZoneOption[] BuildTimeZoneOptions()
    {
        var groups = new (int Offset, string Names)[]
        {
            (-660, "Niue|Pago Pago"),
            (-600, "Honolulu|Rarotonga|Tahiti"),
            (-570, "Marquesas"),
            (-540, "Adak|Gambier"),
            (-480, "Anchorage|Juneau|Metlakatla|Nome|Pitcairn|Sitka|Yakutat"),
            (-420, "Dawson|Dawson Creek|Fort Nelson|Hermosillo|Los Angeles|Mazatlan|Phoenix|Tijuana|Vancouver|Whitehorse"),
            (-360, "Bahia Banderas|Belize|Boise|Cambridge Bay|Chihuahua|Ciudad Juarez|Costa Rica|Denver|Easter|Edmonton|El Salvador|Galapagos|Guatemala|Inuvik|Managua|Merida|Mexico City|Monterrey|Regina|Swift Current|Tegucigalpa"),
            (-300, "Beulah|Bogota|Cancun|Center|Chicago|Eirunepe|Guayaquil|Jamaica|Knox|Lima|Matamoros|Menominee|New Salem|Ojinaga|Panama|Rankin Inlet|Resolute|Rio Branco|Tell City|Winnipeg"),
            (-240, "Barbados|Boa Vista|Campo Grande|Caracas|Cuiaba|Detroit|Grand Turk|Guyana|Havana|Indianapolis|Iqaluit|La Paz|Louisville|Manaus|Marengo|Martinique|Monticello|New York|Petersburg|Port-au-Prince|Porto Velho|Puerto Rico|Santiago|Santo Domingo|Toronto|Vevay|Vincennes|Winamac"),
            (-180, "Araguaina|Asuncion|Bahia|Belem|Bermuda|Buenos Aires|Catamarca|Cayenne|Cordoba|Coyhaique|Fortaleza|Glace Bay|Goose Bay|Halifax|Jujuy|La Rioja|Maceio|Mendoza|Moncton|Montevideo|Palmer|Paramaribo|Punta Arenas|Recife|Rio Gallegos|Rothera|Salta|San Juan|San Luis|Santarem|Sao Paulo|Stanley|Thule|Tucuman|Ushuaia"),
            (-150, "St. John's"),
            (-120, "Miquelon|Noronha|South Georgia"),
            (-60, "Cape Verde|Nuuk|Scoresbysund"),
            (0, "Abidjan|Azores|Bissau|Danmarkshavn|Monrovia|Sao Tome|UTC"),
            (60, "Algiers|Canary|Casablanca|Dublin|El Aaiun|Faroe|Lagos|Lisbon|London|Madeira|Ndjamena|Tunis"),
            (120, "Andorra|Belgrade|Berlin|Brussels|Budapest|Ceuta|Gibraltar|Johannesburg|Juba|Kaliningrad|Khartoum|Madrid|Malta|Maputo|Paris|Prague|Rome|Tirane|Tripoli|Troll|Vienna|Warsaw|Windhoek|Zurich"),
            (180, "Amman|Athens|Baghdad|Beirut|Bucharest|Cairo|Chisinau|Damascus|Famagusta|Gaza|Hebron|Helsinki|Istanbul|Jerusalem|Kirov|Kyiv|Minsk|Moscow|Nairobi|Nicosia|Qatar|Riga|Riyadh|Simferopol|Sofia|Tallinn|Vilnius|Volgograd"),
            (210, "Tehran"),
            (240, "Astrakhan|Baku|Dubai|Mauritius|Samara|Saratov|Tbilisi|Ulyanovsk|Yerevan"),
            (270, "Kabul"),
            (300, "Almaty|Aqtau|Aqtobe|Ashgabat|Atyrau|Dushanbe|Karachi|Maldives|Mawson|Oral|Qostanay|Qyzylorda|Samarkand|Tashkent|Vostok|Yekaterinburg"),
            (330, "Colombo|Kolkata"),
            (345, "Kathmandu"),
            (360, "Bishkek|Chagos|Dhaka|Omsk|Thimphu|Urumqi"),
            (390, "Yangon"),
            (420, "Bangkok|Barnaul|Davis|Ho Chi Minh|Hovd|Jakarta|Krasnoyarsk|Novokuznetsk|Novosibirsk|Pontianak|Tomsk"),
            (480, "Casey|Hong Kong|Irkutsk|Kuching|Macau|Makassar|Manila|Perth|Shanghai|Singapore|Taipei|Ulaanbaatar"),
            (525, "Eucla"),
            (540, "Chita|Dili|Jayapura|Khandyga|Palau|Pyongyang|Seoul|Tokyo|Yakutsk"),
            (570, "Adelaide|Broken Hill|Darwin"),
            (600, "Brisbane|Guam|Hobart|Lindeman|Macquarie|Melbourne|Port Moresby|Sydney|Ust-Nera|Vladivostok"),
            (630, "Lord Howe"),
            (660, "Bougainville|Efate|Guadalcanal|Kosrae|Magadan|Norfolk|Noumea|Sakhalin|Srednekolymsk"),
            (720, "Anadyr|Auckland|Fiji|Kamchatka|Kwajalein|Nauru|Tarawa"),
            (765, "Chatham"),
            (780, "Apia|Fakaofo|Kanton|Tongatapu"),
            (840, "Kiritimati"),
        };

        return groups.SelectMany(
            group => group.Names.Split('|')
                .Select(name => new TimeZoneOption(group.Offset, name))
        ).ToArray();
    }
}
