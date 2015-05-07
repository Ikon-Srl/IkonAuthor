
SET metal="C:\Program Files\Microsoft SDKs\Windows\v6.0A\bin\SqlMetal.exe"
SET connStr="Data Source=dev64.ikon.local;Initial Catalog=Limoni_CMS;User='sa';Password='Hzlmasql_9';Connect Timeout=60;MultipleActiveResultSets=true"
SET baseCmd=%metal% /conn:%connStr% /pluralize /serialization:Unidirectional /namespace:Ikon.GD /context:IKGD_DataContext

%baseCmd% /dbml:"%~dp0test.dbml"
%baseCmd% /code:"%~dp0test01.cs"
%baseCmd% /code:"%~dp0test02.cs" /map:"%~dp0test02.xml"
