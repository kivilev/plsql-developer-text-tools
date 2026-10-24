namespace TextTools.Core.Formatting;

/// <summary>
/// The text the settings window formats for its preview. It is written the way the IDE's Beautifier leaves code, and it gives
/// every rule something to do: switching any setting off changes the preview (a test checks that).
/// </summary>
public static class FormatSample
{
    public static readonly string Text =
        "CREATE OR REPLACE PACKAGE BODY demo_api IS\n" +
        "  -- Opens an account (nvl and %type in a comment stay as they are)\n" +
        "  FUNCTION get_name(i_id IN NUMBER) RETURN VARCHAR2\n" +
        "    RESULT_CACHE\n" +
        "    IS\n" +
        "    v_name VARCHAR2(100);\n" +
        "    v_long_name_x NUMBER;   \n" +
        "    c_limit CONSTANT NUMBER := 10;\n" +
        "  BEGIN\n" +
        "      -- the name of the account\n" +
        "    v_name:=nvl( i_id,0 );\n" +
        "    v_long_name_x:=substr(v_name,1,10);\n" +
        "    RETURN v_name;\n" +
        "  END;\n" +
        "  PROCEDURE open_account(i_id IN NUMBER) IS\n" +
        "    v_acc account%rowtype;\n" +
        "    v_text VARCHAR2(200);\n" +
        "  BEGIN\n" +
        "    INSERT INTO deposit_access (dep_id, branch, descrip) VALUES (i_id, i_branch, i_descrip);\n" +
        "    UPDATE account t\n" +
        "       SET t.status = account_const.c_status_sent,\n" +
        "           t.modify_date = SYSDATE\n" +
        "     WHERE t.id = i_id;\n" +
        "    SELECT a.id,\n" +
        "           a.branch\n" +
        "      INTO v_acc.id,\n" +
        "           v_acc.branch\n" +
        "      FROM account a\n" +
        "     WHERE a.client IN (\n" +
        "       SELECT c.id\n" +
        "         FROM client c\n" +
        "        WHERE c.branch = i_branch\n" +
        "     );\n" +
        "    OPEN c FOR\n" +
        "      SELECT a.id, a.branch, c.name client_name\n" +
        "        FROM account a\n" +
        "        LEFT JOIN client c\n" +
        "            ON c.branch = a.branch\n" +
        "          AND c.id_client = a.client\n" +
        "       WHERE a.branch = i_branch\n" +
        "         AND (a.currency = i_currency\n" +
        "          OR i_currency IS NULL)\n" +
        "         AND a.operation_date BETWEEN i_period_begin_date_of_report AND i_period_end_date_of_report;\n" +
        "    OPEN c2 FOR\n" +
        "      SELECT a.id" + new string(' ', 112) + "alias_one,\n" +
        "             a.very_long_column_name  alias_two\n" +
        "        FROM account a;\n" +
        "    IF v_acc.id = 1\n" +
        "       AND v_acc.branch = 2 THEN\n" +
        "      NULL;\n" +
        "    END IF;\n" +
        "    IF v_acc.state = account_const.c_state_active AND v_acc.currency = i_currency OR i_currency IS NULL THEN\n" +
        "      v_text := DECODE(v_acc.state, 0, 'Created', 1, 'Accepted for processing', 2, 'Rejected by the bank', 3, 'Paid', 'Unknown');\n" +
        "    END IF;\n" +
        "    account_kernel.close_account(i_branch => v_branch, i_client => v_client, i_currency => v_currency, o_code => v_code);\n" +
        "    v_text := 'Account ' || v_acc.id || ' of the client ' || v_acc.client || ' is closed on ' || v_date;\n" +
        "    EXECUTE IMMEDIATE 'update account set state = 1 where id = :1 and branch = :2' USING i_id;\n" +
        "    -- before end\n" +
        "  END;\n" +
        "\n" +
        "END;";
}
