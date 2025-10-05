# === Light Reflective Mirror Server Utilities ===

# 重新编译并安装，不改变当前目录
rebuild() {
    local CURRENT_DIR
    CURRENT_DIR=$(pwd)
    echo "🔧 Rebuilding Light Reflective Mirror Server..."
    
    (
        cd ~/Light-Reflective-Mirror/ServerProject-DONT-IMPORT-INTO-UNITY/ || exit 1
        git fetch origin
        git rebase origin/dev || { echo "❌ Git rebase failed!"; exit 1; }
        
        dotnet restore || { echo "❌ dotnet restore failed!"; exit 1; }
        dotnet build -c Release || { echo "❌ dotnet build failed!"; exit 1; }

        cp LRM/bin/Release/net7.0/LRM.dll ~/
        cp MultiCompiled/bin/Release/net7.0/MultiCompiled.dll ~/
        echo "✅ Build complete and DLLs copied to home directory."
    )

    cd "$CURRENT_DIR" || exit
}

# 启动服务器并记录日志（带时间戳）
startserver() {
    local TIMESTAMP
    TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")
    local LOGFILE="server_${TIMESTAMP}.log"

    echo "🚀 Starting server, logging to ${LOGFILE}..."
    nohup dotnet ~/LRM.dll > "${LOGFILE}" 2>&1 &
    sleep 1
    tail -f "${LOGFILE}"
}

# 停止服务器
stopserver() {
    echo "🛑 Stopping running dotnet server(s)..."
    local PIDS
    PIDS=$(pgrep -f "dotnet .*LRM.dll")
    if [ -z "$PIDS" ]; then
        echo "No LRM server processes found."
    else
        echo "$PIDS" | xargs kill -9
        echo "✅ Server stopped."
    fi
}

# 重启服务器
restartserver() {
    echo "♻️ Restarting server..."
    stopserver
    sleep 2
    startserver
}