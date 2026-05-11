using System.Text.RegularExpressions;
using SmartEnergyExpert.Client.Services;

namespace SmartEnergyExpert.Client.Reporting;

/// <summary>Українські підписи для рекомендацій API (англійські рядки з ComparisonService не показуємо в UI).</summary>
public static class RecommendationCopyUa
{
    public static string SeverityLabel(string severity)
    {
        var s = severity.Trim();
        return s.Equals("low", StringComparison.OrdinalIgnoreCase) ? "НИЗЬКА"
            : s.Equals("moderate", StringComparison.OrdinalIgnoreCase) ? "ПОМІРНА"
            : s.Equals("high", StringComparison.OrdinalIgnoreCase) ? "ВИСОКА"
            : s.Equals("critical", StringComparison.OrdinalIgnoreCase) ? "КРИТИЧНА"
            : severity.ToUpperInvariant();
    }

    public static string CategoryTitle(string category) => category switch
    {
        "ENVIRONMENT_VARIANCE" => "Середовище / SSP і зв’язок з шумом",
        "SENSOR_DRIFT" => "Зміщення калібрування сенсора",
        "MODEL_MISMATCH" => "Глобальна невідповідність моделі поширення",
        "NOISE_INTERFERENCE" => "Широкохвильовий шум / епізодичне маскування",
        "FREQUENCY_ATTENUATION" => "Хвильове згасання / нахил затухання",
        "ACCEPTABLE_MODEL" => "Прийнятна узгодженість моделі",
        "DATA_ALIGNMENT" => "Узгодження даних / спаровування",
        _ => category
    };

    public static string InferenceMethodLabel(string method) =>
        string.Equals(method, "rule_engine_v1", StringComparison.OrdinalIgnoreCase)
            ? "рушій правил (версія 1)"
            : method;

    public static string ReasonTitle(string reasonCode) => reasonCode switch
{
    "NO_JOINABLE_PAIRS" => "немає сумісних пар зразків",
    "EXPERIMENT_PROGRESS_PAIRING" => "парування за прогресом експерименту",
    "FREQ_BAND_ATTENUATION" => "хвильове згасання / частотна розбіжність",
    "GLOBAL_MODEL_MISMATCH" => "дифузна невідповідність моделі",
    "SENSOR_GAIN_BIAS" => "зміщення посилення сенсора",
    "ENVIRONMENT_PROFILE_SHIFT" => "зсув профілю середовища",
    "BROADBAND_NOISE_COUPLED" => "широкохвильовий шум",
    "MODEL_WITHIN_VARIANCE" => "модель у межах очікуваної варіації",
    _ => reasonCode.Replace('_', ' ')
};

public static string Explanation(RecommendationDto rec) => rec.ReasonCode switch
{
    "NO_JOINABLE_PAIRS" =>
        "Нуль спарованих зразків після узгодження хвиль частот, пошуку збігів за часом і парування за прогресом експерименту — залишкові метрики не нараховані.",
    "EXPERIMENT_PROGRESS_PAIRING" =>
        "Застосовано автоматичне парування за прогресом експерименту (нормалізована частка часу в межах кожного набору): доречно, коли обидва ряди мають ту саму фазову структуру без спільної прив’язки до календарного UTC.",
    "FREQ_BAND_ATTENUATION" =>
        "Ймовірна невідповідність згасання або хвильово обмеженого зв’язку між моделлю та вимірами на окремих частотах.",
    "GLOBAL_MODEL_MISMATCH" =>
        "Структура симуляції розходиться з полем «розмазано» — не як один вузький сплеск на одній частоті.",
    "SENSOR_GAIN_BIAS" =>
        "Систематичний зсув амплітуди, сумісний із застарілою калібруванням гідрофона чи передавача.",
    "ENVIRONMENT_PROFILE_SHIFT" =>
        "Багато локальних розбіжностей часто слідують за зміною параметрів середовища відносно тієї сцени, що закладена в моделі.",
    "BROADBAND_NOISE_COUPLED" =>
        "Узгоджені залишки на кількох хвилях натякають на епізодичне наведення шуму або маскування.",
    "MODEL_WITHIN_VARIANCE" =>
        "Спостережувані відмінності залишаються в межах очікуваної експериментальної варіації для цього набору даних.",
    _ => rec.Explanation
};

public static string SuggestedAction(RecommendationDto rec)
{
    return rec.ReasonCode switch
    {
        "NO_JOINABLE_PAIRS" => rec.SuggestedAction.Contains("Harmonize", StringComparison.Ordinal)
            ? "Узгодьте одиниці стовпця частоти між виводом симулятора та польовим логером (Гц проти кодування «кГц», кратність 10), потім повторіть спробу."
            : "Забезпечте узгоджені рядки за частотою з обох боків (після масштабування одиниць), достатньо міток часу там, де потрібно, та вирівняйте часові шкали CSV або спільну епоху.",
        "EXPERIMENT_PROGRESS_PAIRING" =>
            "Якщо публікуєте помилки по точках, за бажанням повторіть імпорт обох рядів на однаковому UTC або на спільних секундах від одного t₀ експерименту — тоді точкове парування замінить режим прогресу.",
        "FREQ_BAND_ATTENUATION" =>
            "Перекалібруйте підсилення по хвилях, поглинання залежно від частоти та спрямовану відповідь перед повним повторним акустичним прогоном.",
        "GLOBAL_MODEL_MISMATCH" =>
            "Перегляньте вхідні дані моделі поширення (SSP, батиметрія, втрати на межах) і звірте з польовими CTD та журналами шуму.",
        "SENSOR_GAIN_BIAS" =>
            "Перевірте каскад посилення опорним тоном і оновіть таблиці калібрування.",
        "ENVIRONMENT_PROFILE_SHIFT" =>
            "Оновіть припущення щодо середовища (швидкість звуку, втрати на поверхні/дні, шум) і повторіть прогон з польовими опорними даними, де вони є.",
        "BROADBAND_NOISE_COUPLED" =>
            "Перевірте цикли роботи обладнання, буксирування, судновий рух; застосуйте адаптивне фільтрування або маски пропускання.",
        "MODEL_WITHIN_VARIANCE" =>
            "Продовжуйте моніторинг; розширте валідацію перед тим як підвищувати довіру до моделі.",
        _ => rec.SuggestedAction
    };
}

public static string EvidenceLine(string line)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        return line;
    }

    var m = Regex.Match(line,
        @"^High mismatch weight concentrated around (.+?) \(weighted share (.+?) of flagged points\)$");
    if (m.Success)
    {
        return $"Висока вага розбіжностей зосереджена біля {BandUa(m.Groups[1].Value)} (вагова частка {m.Groups[2].Value} позначених точок).";
    }

    m = Regex.Match(line, @"^Global mean relative error is ([0-9.]+)%$");
    if (m.Success)
    {
        return $"Середня відносна помилка по прогону: {m.Groups[1].Value}%.";
    }

    m = Regex.Match(line,
        @"^High-error samples cluster tightly in time \(σ ≈ ([0-9.]+)s\), suggesting structured events$");
    if (m.Success)
    {
        return $"Зразки з великою помилкою щільні в часі (σ ≈ {m.Groups[1].Value} с) — натяк на структуровані події.";
    }

    m = Regex.Match(line,
        @"^High-error samples are temporally diffuse \(σ ≈ ([0-9.]+)s\), suggesting environmental drift$");
    if (m.Success)
    {
        return $"Зразки з великою помилкою розмазані в часі (σ ≈ {m.Groups[1].Value} с) — натяк на зміну середовища.";
    }

    m = Regex.Match(line,
        @"^Mean relative error ([0-9.]+)% spreads across bands \(concentration (.+)\)$");
    if (m.Success)
    {
        return $"Середня відносна помилка {m.Groups[1].Value}% розмазана по хвилях (концентрація {m.Groups[2].Value}).";
    }

    m = Regex.Match(line, @"^MAE ([0-9.]+) dB indicates broad amplitude shift, not a single narrowband spike$");
    if (m.Success)
    {
        return $"MAE {m.Groups[1].Value} дБ вказує на широкий зсув амплітуди, а не один вузькохвильовий сплеск.";
    }

    m = Regex.Match(line, @"^Multi-band elevated minutes: (\d+)$");
    if (m.Success)
    {
        return $"Хвилин із підвищеним рівнем на кількох хвилях: {m.Groups[1].Value}.";
    }

    m = Regex.Match(line, @"^MAE ([0-9.]+) dB exceeds calibration drift guard band$");
    if (m.Success)
    {
        return $"MAE {m.Groups[1].Value} дБ перевищує допустимий коридор «дрейфу калібрування».";
    }

    m = Regex.Match(line,
        @"^Relative error coefficient of variation ([0-9.]+) among flagged points$");
    if (m.Success)
    {
        return $"Коефіцієнт варіації відносної помилки серед позначених точок: {m.Groups[1].Value}.";
    }

    m = Regex.Match(line,
        @"^Significant disagreement on (.+?) of samples \((\d+)/(\d+)\)$");
    if (m.Success)
    {
        return $"Суттєва розбіжність на {m.Groups[1].Value} зразків ({m.Groups[2].Value}/{m.Groups[3].Value}).";
    }

    m = Regex.Match(line,
        @"^Elevated minutes span (\d+) distinct intervals with multi-band activity$");
    if (m.Success)
    {
        return $"Підвищені хвилини охоплюють {m.Groups[1].Value} окремих інтервалів із активністю на кількох хвилях.";
    }

    m = Regex.Match(line,
        @"^Simultaneous lift on ≥3 bands within the same minute occurred (\d+) times$");
    if (m.Success)
    {
        return $"Одночасне підвищення на ≥3 хвилях у межах однієї хвилини: {m.Groups[1].Value} разів.";
    }

    if (line.Equals(
            "Pattern matches broadband interference rather than isolated frequency tilt",
            StringComparison.Ordinal))
    {
        return "Картина відповідає широкохвильовим завадам, а не ізольованому нахилу по одній частоті.";
    }

    m = Regex.Match(line, @"^Residuals centered \(MRE ([0-9.]+)%\) with only (.+?) significant share$");
    if (m.Success)
    {
        return $"Залишки зосереджені (MRE {m.Groups[1].Value}%), частка суттєвих лише {m.Groups[2].Value}.";
    }

    if (line.Equals(
            "Per-point explanations reference routine geolocation and depth tolerances",
            StringComparison.Ordinal))
    {
        return "Пояснення по точках спираються на типові допуски за геолокацією та глибиною.";
    }

    m = Regex.Match(line, @"^Simulation: (\d+) samples; field: (\d+) samples\.$");
    if (m.Success)
    {
        return $"Симуляція: {m.Groups[1].Value} зразків; поле: {m.Groups[2].Value} зразків.";
    }

    m = Regex.Match(line, @"^Simulation distinct frequency_band values: (.+)$");
    if (m.Success)
    {
        return $"Унікальні хвилі симуляції: {HzListUa(m.Groups[1].Value)}.";
    }

    m = Regex.Match(line, @"^Field distinct frequency_band values: (.+)$");
    if (m.Success)
    {
        return $"Унікальні хвилі поля: {HzListUa(m.Groups[1].Value)}.";
    }

    if (line.StartsWith("Distinct frequency_band sets do not overlap", StringComparison.Ordinal))
    {
        return "Набори хвиль частот не перетинаються точно й не збігаються після спільного масштабування на порядок (наприклад 6250 Гц проти 62500 Гц).";
    }

    if (line.StartsWith("Exact band overlap:", StringComparison.Ordinal))
    {
        return "Є точний перетин хвиль, але парування за UTC-вікном і за прогресом експерименту не дало жодної пари.";
    }

    if (line.Contains("Bands align by decade scaling; experiment-progress", StringComparison.Ordinal))
    {
        return "Хвилі узгоджуються масштабуванням на порядок; парування за прогресом відпрацювало, але придатних пар не виникло (рідкі рядки або немає перекриття хвиль з одного боку).";
    }

    if (line.StartsWith("Bands can align by decade scaling, but nearest UTC", StringComparison.Ordinal))
    {
        return "Хвилі можна узгодити масштабуванням на порядок, але найближчі сусіди за UTC перевищують адаптивний ліміт перекосу.";
    }

    if (line.Equals("No band alignment after exact match and decade scaling heuristics.", StringComparison.Ordinal))
    {
        return "Після точного збігу та евристики масштабування на порядок узгодити хвилі не вдалося.";
    }

    if (line.StartsWith("Pairing order:", StringComparison.Ordinal))
    {
        return "Порядок парування: точний збіг (UTC × хвиля) → найближчий UTC у межах перекосу з масштабом хвилі → найближчий сусід поля за мінімумом |u_сим − u_поле|, де u ∈ [0,1] — нормалізований пройдений час у межах кожного файлу.";
    }

    if (line.Equals(
            "Exact (UTC × band) and UTC-window nearest did not yield pairs.",
            StringComparison.Ordinal))
    {
        return "Точний збіг (UTC × хвиля) і найближчі в межах UTC-вікна не дали пар.";
    }

    if (line.StartsWith("Each simulation sample u_sim", StringComparison.Ordinal))
    {
        return "Кожен зразок симуляції u_сим — частка часу в [0,1] у межах її CSV; зіставляється з рядком поля, що мінімізує |u_поле − u_сим|, коли щільності подібні; календарні епохи не зводяться автоматично.";
    }

    if (line.StartsWith("If the field trace is much denser", StringComparison.Ordinal))
    {
        return "Якщо поле набагато густіше за симуляцію в тій самій хвилі (≥8×), прототип ставить ряди симуляції на рівномірних квантилях часу впорядкованого поля, щоб кожна точка моделі відповідала іншому вікну спостереження.";
    }

    m = Regex.Match(line, @"^Compared (\d+) residual points; MAE ([0-9.]+) dB, MRE ([0-9.]+)%\.$");
    if (m.Success)
    {
        return $"Порівняно {m.Groups[1].Value} залишкових точок; MAE {m.Groups[2].Value} дБ, MRE {m.Groups[3].Value}%.";
    }

    return line;
}

private static string BandUa(string raw) =>
    raw.Trim().Equals("dominant band", StringComparison.OrdinalIgnoreCase)
        ? "основна хвиля"
        : raw.Replace(" Hz", " Гц", StringComparison.OrdinalIgnoreCase);

private static string HzListUa(string tail) => tail.Replace("(none)", "(немає)", StringComparison.Ordinal);
}
