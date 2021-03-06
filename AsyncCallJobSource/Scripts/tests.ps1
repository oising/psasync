if ($host.name -ne "ConsoleHost") {
	write-warning "Console only, or you'll force shutdown of VS since the assembly will be loaded into the shared appdomain, locking it."
	exit 1
}
add-type -path C:\local\psasync\Async.Tests\bin\Debug\Async.Tests.dll

$begin = [func[asynccallback, object, iasyncresult]]{ [async.tests.asyncresultnoresult]::CreateWithEmptyCallback($null) }
$end = [action[iasyncresult]]{ [diagnostics.trace]::WriteLine("end", "test"); }

$scheduler = new-Object async.tests.nulltaskscheduler
$factory = new-object system.threading.tasks.taskfactory $scheduler
[system.threading.tasks.task]$task = $factory.fromasync($begin, $end, <# state #>$null)

