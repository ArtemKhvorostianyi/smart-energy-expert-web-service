namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.LayoutDashboard, title: "Dashboard", searchHints: ["dashboard", "risk", "overview", "evaluation"])]
public sealed class DashboardApp : ViewBase
{
    public override object? Build()
    {
        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H1("Smart Energy Expert")
               | Text.Markdown("Web-сервіс експертного оцінювання для підтримки прийняття рішень в енергетичних кібер-фізичних системах.")
               | new Separator()
               | Layout.Horizontal().Gap(2)
                   | new Card(
                       Layout.Vertical().Gap(1)
                       | Text.H3("Модулі MVP")
                       | Text.Block("1. Аутентифікація та ролі")
                       | Text.Block("2. Експерименти та параметри")
                       | Text.Block("3. Експертне оцінювання")
                       | Text.Block("4. Рекомендації та звіти")
                   )
                   | new Card(
                       Layout.Vertical().Gap(1)
                       | Text.H3("Рівні ризику")
                       | Text.Block("0.00 - 0.25 : Low")
                       | Text.Block("0.26 - 0.50 : Moderate")
                       | Text.Block("0.51 - 0.75 : High")
                       | Text.Block("0.76 - 1.00 : Critical")
                   )
               | new Separator()
               | Text.Markdown("Для початку відкрий додатки **Experiments** та **Evaluations** у вкладках App Shell.");
    }
}
