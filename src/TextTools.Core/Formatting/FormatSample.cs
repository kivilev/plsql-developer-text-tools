namespace TextTools.Core.Formatting;

/// <summary>
/// The text the settings window formats for its preview. It is written the way the IDE's Beautifier leaves code, and it gives
/// every rule something to do: switching any setting off changes the preview (a test checks that).
/// </summary>
public static class FormatSample
{
    public static readonly string Text =
        "CREATE OR REPLACE PACKAGE BODY demo_api IS\n" +
        "  FUNCTION get_name(i_id IN NUMBER) RETURN VARCHAR2\n" +
        "    RESULT_CACHE\n" +
        "    IS\n" +
        "    v_name VARCHAR2(100);\n" +
        "    v_len NUMBER;   \n" +
        "  BEGIN\n" +
        "      -- nvl in a comment stays as it is\n" +
        "    v_name:=nvl( i_id,0 );\n" +
        "    v_len:=substr(v_name,1,10);\n" +
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
        "    OPEN c3 FOR\n" +
        "      SELECT a.id          account_id, -- key\n" +
        "             a.branch      branch_code,\n" +
        "             c.name        client_name\n" +
        "        FROM account a;\n" +
        "    OPEN c4 FOR\n" +
        "      SELECT\n" +
        "      -- the key first\n" +
        "             a.id,\n" +
        "             a.branch\n" +
        "        FROM account a;\n" +
        "    SELECT a.id\n" +
        "      INTO\n" +
        "      v_id\n" +
        "      FROM account a\n" +
        "     WHERE a.id = i_id;\n" +
        "    IF v_acc.state = account_const.c_state_active AND v_acc.currency = i_currency\n" +
        "       OR i_currency IS NULL THEN\n" +
        "      v_text := DECODE(v_acc.state, 0, 'Created', 1, 'Accepted for processing', 2, 'Rejected by the bank', 3, 'Paid', 'Unknown');\n" +
        "    END IF;\n" +
        "    account_kernel.close_account(i_branch => v_branch, i_client => v_client, i_currency => v_currency, o_code => v_code);\n" +
        "    v_text := 'Account ' || v_acc.id || ' of the client ' || v_acc.client || ' is closed on ' || v_date;\n" +
        "    EXECUTE IMMEDIATE 'update account set state = 1 where id = :1 and branch = :2' USING i_id;\n" +
        "    -- before end\n" +
        "  END;\n" +
        "  PROCEDURE demo_rules(i_branch IN VARCHAR2) IS\n" +
        "    TYPE t_rec IS RECORD(\n" +
        "      id   NUMBER,\n" +
        "      name VARCHAR2(80));\n" +
        "    v_flag NUMBER;\n" +
        "  BEGIN\n" +
        "\n" +
        "    v_flag := 1;      -- set\n" +
        "    SELECT CASE\n" +
        "             WHEN a.state = 1 THEN\n" +
        "              'A'\n" +
        "             ELSE\n" +
        "              'I'\n" +
        "           END\n" +
        "      INTO v_text\n" +
        "      FROM ACCOUNT     a\n" +
        "      JOIN client_data c\n" +
        "        ON c.id = a.client\n" +
        "     WHERE a.code IN (- 4068, - 4061)\n" +
        "       AND rownum = 1;\n" +
        "    SELECT a.id\n" +
        "      BULK COLLECT\n" +
        "      INTO v_ids\n" +
        "      FROM account a;\n" +
        "    SELECT jt.code\n" +
        "      INTO v_code\n" +
        "      FROM JSON_TABLE(v_json,\n" +
        "                      '$'\n" +
        "                      COLUMNS(code NUMBER PATH '$.code')) jt;\n" +
        "    SELECT JSON_ARRAYAGG(JSON_OBJECT('id' VALUE a.id,\n" +
        "                                     'data' VALUE a.data\n" +
        "                                     FORMAT JSON))\n" +
        "      INTO v_json\n" +
        "      FROM account a;\n" +
        "    some_pkg.init_all(NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);\n" +
        "    IF v_flag = 1 AND v_code = 2\n" +
        "       AND v_rest = 3 THEN\n" +
        "      v_flag := 3;\n" +
        "    END IF;\n" +
        "    IF v_code IN (-4068, -4061, -4065, -6508, -6512, -20001, -20002, -20003, -20004) THEN\n" +
        "      v_flag := 2;\n" +
        "    END IF;\n" +
        "    v_text := deposit_checks.is_valid_new_book_dep_params(i_branch, i_filial, i_code);\n" +
        "    v_total := account_data.get_saldo_out(i_branch, 3) + account_data.get_saldo_out(i_branch, 8) + v_rest;\n" +
        "  END;\n" +
        "\n" +
        "END;";
}
