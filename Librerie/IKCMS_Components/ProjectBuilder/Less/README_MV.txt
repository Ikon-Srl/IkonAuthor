
references:
https://github.com/duncansmart/less.js-windows
http://winless.org/build-event-script

per integrare il prebuild dei .less in VS*
nelle procedure di prebuild del progetto web usare:

CALL "$(SolutionDir)..\IkonPortal\Librerie\IKCMS_Components\ProjectBuilder\Less\lessc.cmd" "$(ProjectDir)Content\LESS\SRC" "$(ProjectDir)Content\LESS\CSS" -compress
EXIT /B 0

