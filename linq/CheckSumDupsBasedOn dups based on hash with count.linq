<Query Kind="Expression">
  <Connection>
    <ID>a0e7b2bd-baa1-4ecd-bba0-f691853639c3</ID>
    <NamingServiceVersion>2</NamingServiceVersion>
    <Persist>true</Persist>
    <Server>SNOWBALL\MSSQLSERVER01</Server>
    <Database>Photos</Database>
  </Connection>
  <Output>DataGrids</Output>
</Query>

(from p in CheckSumDupsBasedOns where p.DupBasedOn == "ShaHash" group p by p.BasedOnVal into g select new {g.Key, Count = g.Count()}).Take(10)