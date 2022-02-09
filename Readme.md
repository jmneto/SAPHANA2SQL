# SAPHANA2SQL

## Tool to assist with the process of converting SAP HANA database scripts to Microsoft SQL Server syntax scripts

This tool uses a set of rules defined in the "rules file" to perform text replacements to translate database scripts to SQL Server syntax. It allows for simple regular text replacements and complex parameter transposition if required.  

The "rules file" contains the rules, one rule per line, the text to be replaced on the left of the separator char '#', and the new replacement text is on the right of the separator char '#'.  

Although the rules in this example apply to SAP HANA to SQL Server syntax translation, these rules can be modified to make any required syntax translation independent of the source or target database server.

## Regular text replacement Rule:

```
text to replace#new text
```

## Text Replacement plus parameter transposition:
To transpose parameters, use the syntax {0}. In the example below, {0} and {1} are the parameters to be transposed from the original text to the new replacement text. 

In this example if the original script file contains: Text to Replace "parameter1" "parameter2", it wwill be translated to: new text "parameter2" "parameter1".

```
Text to replace "{0}" "{1}"#new text "{1}" "{0}"
```

## Extensibility

You can add your own rules as required for your scenario. 

## Sample Rules File

```
UNLOAD PRIORITY 5  AUTO MERGE#
;#
CS_FIXED#
CS_INT#
COLUMN#
CS_FLOAT#
CS_DAYDATE#
CS_SECONDTIME#
CS_DOUBLE#
CS_LONGDATE#
DOUBLE#FLOAT
LONGDATE#DATETIME2(7)
BLOB MEMORY THRESHOLD 1000#varbinary(max)
CURRENTDATE#GetDate()
NVARCHAR(5000)#NVARCHAR(MAX)
ROW#
CS_STRING#
NCLOB MEMORY THRESHOLD 1000#varbinary(max)
STRING#
%COMMENT ON TABLE "{0}"."{1}" is '{2}'#EXEC sys.sp_addextendedproperty @name=N'{1}',@value=N'{2}',@level0type=N'SCHEMA',@level0name=N'{0}',@level1type=N'TABLE',@level1name=N'{1}'
%COMMENT ON  "{0}"."{1}"."{2}" is '{3}'#EXEC sys.sp_addextendedproperty @name=N'{2}',@value=N'{3}',@level0type=N'SCHEMA',@level0name=N'{0}',@level1type=N'TABLE',@level1name=N'{1}',@level2type=N'COLUMN',@level2name=N'{2}'
```

## The sample rules file above would convert a script like below from SAP HANA syntax to a SQL Server compatible syntax:

**Original SAP HANA script file:**

```
CREATE COLUMN TABLE "MYSCHEMA"."MYTABLE" ("CLIENT" NVARCHAR(3) DEFAULT '000' NOT NULL , "JOB" NVARCHAR(4) DEFAULT '' NOT NULL , PRIMARY KEY ("CLIENT", "JOB")) UNLOAD PRIORITY 5  AUTO MERGE ;
COMMENT ON TABLE "MYSCHEMA"."MYTABLE" is 'Occupations';
COMMENT ON COLUMN "MYSCHEMA"."MYTABLE"."CLIENT" is 'Client';
COMMENT ON COLUMN "MYSCHEMA"."MYTABLE"."JOB" is 'Occupation/group'
CONVERTED TO SQL:
```

**Converted to SQL Server Script File:**

```
CREATE  TABLE "MYSCHEMA"."MYTABLE" ("CLIENT" NVARCHAR(3) DEFAULT '000' NOT NULL , "JOB" NVARCHAR(4) DEFAULT '' NOT NULL , PRIMARY KEY ("CLIENT", "JOB"))  
GO
EXEC sys.sp_addextendedproperty @name=N'MYTABLE',@value=N'Occupations',@level0type=N'SCHEMA',@level0name=N'MYSCHEMA',@level1type=N'TABLE',@level1name=N'MYTABLE'
GO
EXEC sys.sp_addextendedproperty @name=N'CLIENT',@value=N'Client',@level0type=N'SCHEMA',@level0name=N'MYSCHEMA',@level1type=N'TABLE',@level1name=N'MYTABLE',@level2type=N'COLUMN',@level2name=N'CLIENT'
GO
EXEC sys.sp_addextendedproperty @name=N'JOB',@value=N'Occupation/group',@level0type=N'SCHEMA',@level0name=N'MYSCHEMA',@level1type=N'TABLE',@level1name=N'MYTABLE',@level2type=N'COLUMN',@level2name=N'JOB'
GO
```

## Usage

```
SAPHANA2SQL -sourcefolder .\SAPHANA2SQL\Data --rules .\SAPHANA2SQL\Data\rules --outputfile .\SAPHANA2SQL\Data\output.txt

 --sourcefolder    Required. Path to root of folder with script files to translate
 --rules           Required. Path to text replacement-rules csv file
 --outputfile      Required. Path to output file
 --help            Display this help screen.

```

## Example

```
SAPHANA2SQL
Source Folder .\SAPHANA2SQL\Data
Rules File .SAPHANA2SQL\Data\rules
Output File .\SAPHANA2SQL\Data\output.txt

Processing Files
.................................................................................................

Writing output to .\SAPHANA2SQL\Data\output.txt

Done.
```

# OS Support

Windows and Linux
