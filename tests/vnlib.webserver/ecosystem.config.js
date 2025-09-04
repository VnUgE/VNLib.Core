module.exports = {
    apps: [{
        name: "vnlib.webserver",
        instances: 1,
        script: "dotnet",
        args: `run
         --configuration debug 
         --no-restore
         --verbosity quiet
         --project "../../apps/VNLib.WebServer/src/"
         --no-launch-profile
         --environment VNLIB_SHARED_HEAP_DIAGNOSTICS="1"
         --
         --verbose
         --config test-config.json`,
        autorestart: false,
        vizion: false,
        combine_logs: true,
        log_file: "server.log",
        env_linux: { },
        env_windows: { },
        env_darwin: { }
  }]
}
