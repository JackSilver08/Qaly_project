@echo off
set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
set ConnectionStrings__DefaultConnection=Server=127.0.0.1;Database=QalyDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
dotnet "%ROOT%\src\Qaly.Web\bin\Debug\net10.0\Qaly.Web.dll" --urls http://127.0.0.1:5000 > "%ROOT%\.tmp\qaly-web.log" 2>&1
