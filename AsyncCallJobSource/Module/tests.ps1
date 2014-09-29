# TODO
write-host "Script tests"

#region waiter type definition
if (-not ("waiter" -as [type])) {
    add-type -Path C:\local\psasync\Async.Tests\bin\Debug\Async.Tests.dll -verbose
}
#endregion

# {} -as ((get-opentypefunc 1 2 3 4 5 6 7 8 9 10 11 12 13 14).makegenerictype(@(0..16| % {[int]}))) | % invoke

[System.Runtime.CompilerServices.CallSite]
[System.Runtime.CompilerServices.CallSite`1]


function new-csarginfo([type]$argType) {
    
    if (!$argType) {
        $flag = "None"    
    } elseif ($argType -in @([int],[single],[double],[bigint])) {
        $flag = "Constant,Usecompiletimetype"
    } else {
        $flag = "usecompiletimetype"
    }
    [Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo]::Create($flag, $null)
}

function get-opentypefunc {
    # 0 args
    # [func[System.AsyncCallback, System.Object, System.IAsyncResult]]
    # open type = [func`3]

    iex ("[func``{0}]" -f ($args.Length + 3))
}

function invoke-generic {

}

$w = new-object Async.Tests.Waiter 1 # one second


$factory = [System.Threading.Tasks.Task]::Factory
$method = "FromAsync"
$begin = $w.BeginDoWaitNoResult

$task = invoke-generic $factory $method $begin $end $state -verbose
