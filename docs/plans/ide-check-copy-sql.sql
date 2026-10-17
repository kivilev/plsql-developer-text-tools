-- Manual check of SQL Tools -> Copy SQL with constants resolved in the real IDE.
-- 1. Connect to a test schema, run this script in a Command Window (creates two packages).
-- 2. Open package body TT_CHECK_BODY in a Program Window, put the cursor in each query, run the command,
--    paste into a SQL Window: expected results are in the comments.

create or replace package tt_check_const is
  c_type_retail constant number := 10;
  c_status_new  constant varchar2(3) := 'NEW';
  c_start       constant date := date '2020-01-01';
  c_flag        constant boolean := true;
  function today return date;
end tt_check_const;
/
create or replace package body tt_check_const is
  function today return date is begin return trunc(sysdate); end;
end tt_check_const;
/
create or replace package tt_check_body is
  procedure run(p_id number);
end tt_check_body;
/
create or replace package body tt_check_body is
  c_local constant pls_integer := 100;

  procedure run(p_id number) is
    l_cnt number;
  begin
    -- (a) offline and online: 'NEW' /*...*/, 100 /*c_local*/, :p_id; INTO removed
    -- connected: 10 /*tt_check_const.c_type_retail*/, date '2020-01-01' /*...*/
    -- not connected: :c_type_retail, :c_start, status bar "(not connected to a database)"
    select count(*)
      into l_cnt
      from dual d
     where 'NEW' = tt_check_const.c_status_new
       and 10 = tt_check_const.c_type_retail
       and sysdate > tt_check_const.c_start
       and rownum <= c_local
       and p_id is not null;

    -- (b) connected: tt_check_const.today stays as is (a function); not connected: becomes :today
    for rec in (select d.dummy from dual d where trunc(sysdate) >= tt_check_const.today) loop
      select count(*) into l_cnt from dual where dummy = rec.dummy;
    end loop;
  end run;
end tt_check_body;
/

-- Checklist (record results in docs/dev-journal.ru.md):
-- [ ] Plug-Ins: SQL Tools submenu with its ribbon icon; the command in Configure > Key Configuration (bind Ctrl+Shift+C)
-- [ ] (a) cursor anywhere in the query, nothing selected: clipboard as expected, editor text unchanged, status bar summary
-- [ ] (a) disconnected (Session > Log off): binds instead of package constants, "(not connected to a database)"
-- [ ] (b) cursor in the inner "select count(*)": rec.dummy -> :rec_dummy; cursor in the FOR loop's select: only that select
-- [ ] select a part of a WHERE clause: only the selection is copied, aliases known (d.dummy untouched)
-- [ ] pasted SQL runs in a SQL Window (binds are asked), Explain Plan (F5) works on it
-- [ ] Cyrillic string constant survives the clipboard
-- [ ] the command while a query is executing in the same window: no hang (DB lookup skipped)

-- cleanup:
-- drop package tt_check_body;
-- drop package tt_check_const;
