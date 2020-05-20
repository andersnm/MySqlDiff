// gplex mysql tokenizer

%using System.Collections;
%namespace MySqlDiff
%visibility internal
%tokentype Token

%option stack, minimize, parser, verbose, persistbuffer, noembedbuffers
%option unicode, codepage:raw

// CREATE CREATE
// TABLE TABLE
IDENTIFIER [a-zA-Z_][a-zA-Z0-9_]*
WS [ \t\r\n]

STRCH [^\'\a\b\n\r\t\v\0]|\\n|\\r
// STRCH [^\'\a\b\f\n\r\t\v\0]|\\n|\\r

QIDENTIFIER [^\\`\a\b\f\n\r\t\v\0]*

%x SetDelimiter
%x BeginBody

%{
	bool PeekDelimiter(bool consume) {
		var delimCtx = new Context();
		if (code == CurrentDelimiter[0]) {
			MarkToken();
			SaveStateAndPos(ref delimCtx);

			var isDelimiter = true;
			for (var i = 1; i < CurrentDelimiter.Length; i++) {
				GetCode();

				if (code != CurrentDelimiter[i]) {
					RestoreStateAndPos(ref delimCtx);
					isDelimiter = false;
					break;
				}
			}

			if (!consume) {
				RestoreStateAndPos(ref delimCtx);
			} else {
				MarkEnd();
				GetCode();
			}

			return isDelimiter;
		}
		return false;

	}

%}

%%

%{
	int bodyTokPos = 0, bodyTokLin = 0, bodyTokCol = 0;

	if (PeekDelimiter(true)) {
		// Console.WriteLine("Scan() intercepted dynamic delimiter " + CurrentDelimiter);
		return (int)Token.DELIMITER;
	}
%}

\(				  { return (int)'('; }
\)				  { return (int)')'; }
\,				  { return (int)','; }
\=				  { return (int)'='; }
\@				  { return (int)'@'; }
\#.*$             { ; }
\-\-.*$           { ; }
CREATE            { return (int)Token.CREATE; }
TABLE             { return (int)Token.TABLE; }
BOOL              { return (int)Token.BOOL; }
BOOLEAN           { return (int)Token.BOOLEAN; }
INT               { return (int)Token.INT; }
VARCHAR           { return (int)Token.VARCHAR; }
CHAR              { return (int)Token.CHAR; }
TEXT              { return (int)Token.TEXT; }
TINYTEXT          { return (int)Token.TINYTEXT; }
MEDIUMTEXT        { return (int)Token.MEDIUMTEXT; }
DATETIME          { return (int)Token.DATETIME; }
TINYINT           { return (int)Token.TINYINT; }
SMALLINT          { return (int)Token.SMALLINT; }
BIGINT            { return (int)Token.BIGINT; }
DECIMAL           { return (int)Token.DECIMAL; }
FLOAT             { return (int)Token.FLOAT; }
TIMESTAMP         { return (int)Token.TIMESTAMP; }
DATE              { return (int)Token.DATE; }
TIME              { return (int)Token.TIME; }
NO{WS}ACTION      { return (int)Token.NO_ACTION; }
NOT               { return (int)Token.NOT; }
ON                { return (int)Token.ON; }
UPDATE            { return (int)Token.UPDATE; }
DELETE            { return (int)Token.DELETE; }
INSERT            { return (int)Token.INSERT; }
INTO              { return (int)Token.INTO; }
VALUES            { return (int)Token.VALUES; }
PRIMARY           { return (int)Token.PRIMARY; }
UNIQUE            { return (int)Token.UNIQUE; }
KEY               { return (int)Token.KEY; }
CONSTRAINT		  { return (int)Token.CONSTRAINT; }
FOREIGN			  { return (int)Token.FOREIGN; }
REFERENCES		  { return (int)Token.REFERENCES; }
NULL              { return (int)Token.NULL; }
AUTO_INCREMENT    { return (int)Token.AUTO_INCREMENT; }
DEFAULT           { return (int)Token.DEFAULT; }
CURRENT_TIMESTAMP { return (int)Token.CURRENT_TIMESTAMP; }
CHARACTER{WS}SET  { return (int)Token.CHARACTER_SET; }
COLLATE           { return (int)Token.COLLATE; }
PROCEDURE         { return (int)Token.PROCEDURE; }
TRIGGER           { return (int)Token.TRIGGER; }
AFTER             { return (int)Token.AFTER; }
BEFORE            { return (int)Token.BEFORE; }
IN                { return (int)Token.IN; }
OUT               { return (int)Token.OUT; }
FOR{WS}EACH{WS}ROW { return (int)Token.FOR_EACH_ROW; }

DELIMITER{WS}*    {
	CurrentDelimiter = "";
	yy_push_state(SetDelimiter);
}

BEGIN{WS}+ {
	// Read until END {WS}* CurrentDelimiter
	yy_push_state(BeginBody);
	bodyTokPos = tokPos;
    bodyTokLin = tokLin;
    bodyTokCol = tokCol;
}

{IDENTIFIER}      { return (int)Token.IDENTIFIER; }
`{QIDENTIFIER}`    { return (int)Token.IDENTIFIER; }
\'{STRCH}*\'      { return (int)Token.STRINGLIT; }
[0-9]+            { return (int)Token.NUMLIT; }
[0-9]+\.[0-9]+    { return (int)Token.NUMLIT; }

{WS}+ {
	// ignore whitespace, except if followed by delimiter. PeekDelimiter above isnt called after ignored tokens
	if (PeekDelimiter(true)) {
		return (int)Token.DELIMITER;
	}
}

<SetDelimiter> {
	[^ \t\r\n\0] {
		// Console.WriteLine("CURRENT DELIM: " + TokenSpan().ToString());
		CurrentDelimiter += TokenSpan().ToString();
	}

	{WS}+|<<EOF>> {
		// Console.WriteLine("CURRENT DELIM: " + CurrentDelimiter);
		yy_pop_state();
		return (int)Token.SETDELIMITER;
	}
}

<BeginBody> {
	END{WS}* {
		if (PeekDelimiter(false)) {
			yy_pop_state();
			MarkEnd();
			tokPos = bodyTokPos;
			tokLin = bodyTokLin;
			tokCol = bodyTokCol;

			// Console.WriteLine("TOKSN: " + TokenSpan().ToString());
			return (int)Token.BEGIN_BODY_END;
		}
	}

	. {
		// OK, skip bodys
	}
}

%{
	yylloc = new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer);
%}
