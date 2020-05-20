%using System.Collections;
%namespace MySqlDiff
%tokentype Token

%YYLTYPE LexSpan

%partial
%visibility internal

%union {
	public List<string> tokenList;
	public List<TableElement> columns;
	public TableElement column;
	public string typeString;
	public List<ConstraintAction> actions;
	public ConstraintAction action;
	public List<List<string>> stringListList;
}

%token CREATE UPDATE DELETE INSERT TABLE PROCEDURE TRIGGER 
%token IDENTIFIER NOT NULL AUTO_INCREMENT DEFAULT CURRENT_TIMESTAMP ON CHARACTER_SET COLLATE NO_ACTION VALUES INTO AFTER BEFORE IN OUT FOR_EACH_ROW
%token PRIMARY UNIQUE KEY CONSTRAINT FOREIGN REFERENCES
%token BOOL BOOLEAN INT TINYINT SMALLINT BIGINT DECIMAL FLOAT CHAR VARCHAR DATETIME TIMESTAMP DATE TIME TEXT TINYTEXT MEDIUMTEXT
%token NUMLIT STRINGLIT SETDELIMITER DELIMITER BEGIN_BODY_END

%type <columns> create_table_elements create_table_elements_opt
%type <column> create_table_element
%type <typeString> type null_opt default_opt autoincr_opt charset_opt collate_opt identifier
%type <tokenList> key_fields insert_values
%type <actions> on_constraints on_constraints_opt
%type <action> on_constraint
%type <stringListList> insert_values_rows

%%

program:
	stmt_list {
	}
	;

stmt_list:
	stmt {
	}
	| stmt_list stmt {
	}
	;

stmt:
	SETDELIMITER {
	}
	| CREATE create_table_modifiers_opt PROCEDURE identifier '(' create_table_elements_opt ')' BEGIN_BODY_END DELIMITER {
		Statements.Add(new CreateProcedureStatement() {
			Name = $4,
			Arguments = $6,
			Body = @8.ToString(),
		});
	}
	| CREATE create_table_modifiers_opt TRIGGER identifier trigger_when trigger_stmt ON identifier FOR_EACH_ROW BEGIN_BODY_END DELIMITER {
		Statements.Add(new CreateTriggerStatement() {
			TriggerName = $4,
			TriggerWhen = @5.ToString(),
			TriggerStmt = @6.ToString(),
			TableName = $8,
			Body = @10.ToString(),
		});
	}
	| CREATE TABLE identifier '(' create_table_elements ')' create_table_modifiers_opt DELIMITER {
		Statements.Add(new CreateTableStatement() {
			TableName = $3,
			Columns = $5,
		});
	}
	| INSERT INTO identifier '(' key_fields ')' VALUES insert_values_rows DELIMITER {
		Statements.Add(new InsertStatement() {
			TableName = $3,
			Columns = $5,
			ValueRows = $8,
		});
	};

create_table_modifiers_opt:
	create_table_modifiers {
	} | {
	};

create_table_modifiers:
	create_table_modifier {
	}
	| create_table_modifiers create_table_modifier {
	};

create_table_modifier:
	identifier '=' default_expr {
	}
	| identifier '=' identifier {
	}
	| identifier '=' identifier '@' identifier {
	}
	| COLLATE '=' identifier {
	}
	| AUTO_INCREMENT '=' default_expr {
	}
	| DEFAULT identifier '=' identifier {
	};

insert_values_rows:
	'(' insert_values ')' {
		$$ = new List<List<string>>();
		$$.Add($2);
	}
	| insert_values_rows ',' '(' insert_values ')' {
		$$ = $1;
		$$.Add($4);
	};

insert_values:
	default_expr {
		$$ = new List<string>();
		$$.Add(@1.ToString());
	}
	| insert_values ',' default_expr {
		$$ = $1;
		$$.Add(@3.ToString());
	};

trigger_when:
	BEFORE {
		$$ = $1;
	}
	| AFTER {
		$$ = $1;
	};

trigger_stmt:
	INSERT {
		$$ = $1;
	}
	| UPDATE {
		$$ = $1;
	}
	| DELETE {
		$$ = $1;
	};

create_table_elements_opt:
	create_table_elements {
		$$ = $1;
	}
	| {
		$$ = new List<TableElement>();
	}
	;

create_table_elements:
	create_table_element {
		$$ = new List<TableElement>();
		$$.Add($1);
	}
	| create_table_elements ',' create_table_element {
		$$.Add($3);
	}
	;

sp_arg_in_out_opt:
	IN {
		$$ = $1;
	}
	| OUT {
		$$ = $1;
	}
	| {
	}
	;

create_table_element:
	sp_arg_in_out_opt identifier type null_opt default_opt autoincr_opt on_column_opt {
		var typeParts = new List<string>();
		$$ = new TableColumn() {
			Direction = !String.IsNullOrEmpty(@1.ToString()) ? @1.ToString() : null,
			Name = $2,
			TypeParts = typeParts,
		};

		if ($3 != null) typeParts.Add($3);
		if ($4 != null) typeParts.Add($4);
		if ($5 != null) typeParts.Add($5);
		if ($6 != null) typeParts.Add($6);
	}
	| unique_opt KEY identifier '(' key_fields ')' {
		$$ = new TableKey() {
			Name = $3,
			Columns = $5,
		};
	}
	| PRIMARY KEY '(' key_fields ')' {
		if ($4 == null) throw new InvalidOperationException("KKAKS");
		$$ = new TableKey() {
			Primary = true,
			Columns = $4,
		};
	}
	| CONSTRAINT identifier FOREIGN KEY '(' key_fields ')' REFERENCES identifier '(' key_fields ')' on_constraints_opt {
		$$ = new TableConstraint() {
			Name = $2,
			Columns = $6,
			ReferencesTable = $9,
			ReferencesColumns = $11,
			Actions = $13,
		};
	}
	;

unique_opt:
	UNIQUE {
		$$ = $1; // @1.ToString();
	}
	| {
	}
	;

type:
	BOOL {
		$$ = @1.ToString();
	}
	| BOOLEAN {
		$$ = @1.ToString();
	}
	| INT '(' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
	}
	| INT {
		$$ = @1.ToString();
	}
	| TINYINT '(' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
	}
	| TINYINT {
		$$ = @1.ToString();
	}
	| SMALLINT '(' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
	}
	| SMALLINT {
		$$ = @1.ToString();
	}
	| BIGINT '(' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
	}
	| BIGINT {
		$$ = @1.ToString();
	}
	| FLOAT '(' NUMLIT ',' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ", " + @5.ToString() + ")";
	}
	| FLOAT '(' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
	}
	| FLOAT {
		$$ = @1.ToString();
	}
	| DECIMAL '(' NUMLIT ',' NUMLIT ')' {
		$$ = @1.ToString() + "(" + @3.ToString() + ", " + @5.ToString() + ")";
	}
	| DECIMAL {
		$$ = @1.ToString();
	}
	| DATETIME {
		$$ = @1.ToString();
	}
	| TIMESTAMP {
		$$ = @1.ToString();
	}
	| DATE {
		$$ = @1.ToString();
	}
	| TIME {
		$$ = @1.ToString();
	}
	| TEXT charset_opt collate_opt {
		$$ = @1.ToString();
		if ($2 != null) {
			$$ += " " + @2.ToString();
		}
	}
	| TINYTEXT charset_opt collate_opt {
		$$ = @1.ToString();
		if ($2 != null) {
			$$ += " " + @2.ToString();
		}
	}
	| MEDIUMTEXT charset_opt collate_opt {
		$$ = @1.ToString();
		if ($2 != null) {
			$$ += " " + @2.ToString();
		}
	}
	| VARCHAR '(' NUMLIT ')' charset_opt collate_opt {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
		if ($5 != null) {
			$$ += " " + @5.ToString();
		}
	}
	| CHAR '(' NUMLIT ')' charset_opt collate_opt {
		$$ = @1.ToString() + "(" + @3.ToString() + ")";
		if ($5 != null) {
			$$ += " " + @5.ToString();
		}
	}
	;

collate_opt:
	COLLATE identifier {
		$$ = $1 + " " + @2.ToString();
	}
	| {
	}
	;

charset_opt:
	CHARACTER_SET identifier {
		$$ = @1.ToString() + " " + $2;
	}
	| {
	}
	;

null_opt:
	NULL {
		$$ = @1.ToString();
	}
	| NOT NULL {
		$$ = @1.ToString() + " " + @2.ToString();
	}
	| {
	}
	;

autoincr_opt:
	AUTO_INCREMENT {
		$$ = @1.ToString();
	}
	| {
	}
	;

default_opt:
	DEFAULT default_expr {
		$$ = @1.ToString() + " " + @2.ToString();
	}
	| {
	}
	;

default_expr:
	NULL {
		$$ = $1;
	}
	| NUMLIT {
		$$ = $1;
	}
	| STRINGLIT {
		$$ = $1;
	}
	| CURRENT_TIMESTAMP {
		$$ = $1;
	}
	;

on_column_opt:
	ON UPDATE default_expr {
	}
	| {
	}
	;

on_constraints_opt:
	on_constraints {
		$$ = $1;
	}
	| {
		$$ = new List<ConstraintAction>();
	}
	;

on_constraints:
	on_constraint {
		$$ = new List<ConstraintAction>();
	}
	| on_constraints on_constraint {
		$$ = $1;
		$$.Add($2);
	}
	;

on_constraint:
	ON UPDATE NO_ACTION {
		$$ = new ConstraintAction() {
			OnStmt = @2.ToString(),
			Action = @3.ToString(),
		};
	}
	| ON DELETE NO_ACTION {
		$$ = new ConstraintAction() {
			OnStmt = @2.ToString(),
			Action = @3.ToString(),
		};
	}
	;

key_fields:
	identifier {
		$$ = new List<string>() { $1 };
	}
	| key_fields ',' identifier {
		$$ = $1;
		$$.Add($3);
	}
	;

identifier:
	IDENTIFIER {
		$$ = @1.ToString();
		if ($$[0] == '`') {
			$$ = $$.Substring(1, $$.Length - 2);
		}
	}
	;