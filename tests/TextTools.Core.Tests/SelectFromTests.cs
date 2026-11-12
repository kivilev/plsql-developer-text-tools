using TextTools.Core.Sql;

namespace TextTools.Core.Tests;

public class SelectFromTests
{
    /// <summary>'§' marks the cursor.</summary>
    private static string Run(string marked)
    {
        var cursor = marked.IndexOf('§', StringComparison.Ordinal);
        return SelectFrom.Build(new SqlRequest(marked.Remove(cursor, 1), 0, 0, cursor)).Text;
    }

    [Theory]
    [InlineData("select * from empl§oyees e", "select * from employees")]
    [InlineData("select * from §employees", "select * from employees")]
    [InlineData("delete from employees§;", "select * from employees")]
    [InlineData("delete from employees§ where id = 1", "select * from employees")]
    [InlineData("select * from h§r.employees", "select * from hr.employees")]
    [InlineData("select * from hr.emp§loyees", "select * from hr.employees")]
    [InlineData("select * from hr§.employees", "select * from hr.employees")]
    [InlineData("select * from hr.§employees", "select * from hr.employees")]
    [InlineData("select * from \"Mixed §Case\" t", "select * from \"Mixed Case\"")]
    [InlineData("select * from hr.\"Ta§b\"", "select * from hr.\"Tab\"")]
    [InlineData("select * from emp§@remote.world", "select * from emp@remote.world")]
    [InlineData("select * from emp@rem§ote.world", "select * from emp@remote.world")]
    [InlineData("l_row emp§loyees%rowtype;", "select * from employees")]
    [InlineData("update таб§лица set a = 1", "select * from таблица")]
    [InlineData("insert into\r\n  orders§\r\nvalues (1)", "select * from orders")]
    [InlineData("from sys$t#ab§", "select * from sys$t#ab")]
    public void CopiesSelectFromTheNameUnderTheCursor(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("§")]
    [InlineData("select *   §  from t")]
    [InlineData("sel§ect * from t")]
    [InlineData("select * FR§OM t")]
    [InlineData("where a = 'lit§eral'")]
    [InlineData("where a = 4§2")]
    [InlineData("where a = :bi§nd")]
    [InlineData("where a = :new.c§ol")]
    [InlineData("where a = &su§bst")]
    [InlineData("-- comment on emp§loyees\n")]
    [InlineData("select * from \"unterm§inated")]
    public void RefusesWhereThereIsNoObjectName(string text) =>
        Assert.Throws<FormatException>(() => Run(text));

    [Theory]
    [InlineData("hr.employees", "select * from hr.employees")]
    [InlineData("  employees \n", "select * from employees")]
    [InlineData("\"My Tab\"@db", "select * from \"My Tab\"@db")]
    public void CopiesTheSelectedName(string selected, string expected)
    {
        var text = "x " + selected + " y";
        Assert.Equal(expected, SelectFrom.Build(new SqlRequest(text, 2, selected.Length, 0)).Text);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("select")]
    [InlineData("t where")]
    [InlineData("'x'")]
    [InlineData("hr.")]
    public void RefusesASelectionThatIsNotOneName(string selected) =>
        Assert.Throws<FormatException>(() => SelectFrom.Build(new SqlRequest(selected, 0, selected.Length, 0)));

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(3, 5)]
    [InlineData(0, 9)]
    public void RefusesASelectionOutsideTheText(int start, int length) =>
        Assert.Throws<FormatException>(() => SelectFrom.Build(new SqlRequest("abcde", start, length, 0)));

    [Fact]
    public void TheSummaryIsTheStatement()
    {
        var r = SelectFrom.Build(new SqlRequest("t", 0, 0, 1));
        Assert.Equal("select * from t", r.Summary);
    }

    [Fact]
    public void IsInTheSqlMenuAndTheContextMenuRightAfterExplainPlan()
    {
        var cmd = Assert.Single(Commands.All.OfType<CopyCommand>(), c => string.Equals(c.Name, "Copy SELECT * FROM object", StringComparison.Ordinal));
        Assert.Equal(Commands.SqlMenu, cmd.Menu);
        Assert.Equal("select * from hr.employees", cmd.SampleResult);
        var context = Commands.ContextMenu.ToList();
        Assert.Equal(context.IndexOf("Explain Plan with constants") + 1, context.IndexOf(cmd.Name));
        Assert.Equal(context.IndexOf("List -> IN (...)") - 1, context.IndexOf(cmd.Name));
    }
}
