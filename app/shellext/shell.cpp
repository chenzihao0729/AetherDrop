// SPDX-License-Identifier: GPL-2.0-only
// AetherDrop's entry in the Windows 11 Explorer context menu ("用 AetherDrop 发送"
// on the first level, not under "Show more options"). Windows 11 only shows
// verbs from packaged apps there, so this IExplorerCommand is declared by a
// sparse package (installer\sparse\AppxManifest.xml) whose external location
// is the install folder. Invoking it starts AetherDrop.exe --send with the
// selected paths; the client takes it from there.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <string>
#include <new>

// {79AAAA41-782F-4AF2-B937-4A587C77FABE}, also in AppxManifest.xml.
static const CLSID CLSID_NekoSend = {0x79aaaa41, 0x782f, 0x4af2, {0xb9, 0x37, 0x4a, 0x58, 0x7c, 0x77, 0xfa, 0xbe}};

static HMODULE g_module;
static LONG g_objects;

static std::wstring folder()
{
    wchar_t path[MAX_PATH * 2];
    DWORD n = GetModuleFileNameW(g_module, path, ARRAYSIZE(path));
    std::wstring s(path, n);
    return s.substr(0, s.find_last_of(L'\\'));
}

static std::wstring exe() { return folder() + L"\\AetherDrop.exe"; }

// The client's language choice (HKCU\Software\AetherDrop, Language = zh | en),
// else the Windows display language.
static bool chinese()
{
    wchar_t v[8] = {};
    DWORD size = sizeof v;
    if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\AetherDrop", L"Language", RRF_RT_REG_SZ, nullptr, v, &size) == ERROR_SUCCESS)
        return v[0] == L'z';
    return PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_CHINESE;
}

static void quote(std::wstring &cmd, const std::wstring &arg)
{
    // CommandLineToArgvW rules: backslashes before a quote are doubled.
    cmd += L" \"";
    size_t slashes = 0;
    for (wchar_t c : arg) {
        if (c == L'\\') { slashes++; continue; }
        if (c == L'"') cmd.append(slashes * 2 + 1, L'\\');
        else cmd.append(slashes, L'\\');
        slashes = 0;
        cmd += c;
    }
    cmd.append(slashes * 2, L'\\');
    cmd += L'"';
}

class SendCommand final : public IExplorerCommand {
    LONG _refs = 1;
public:
    SendCommand() { InterlockedIncrement(&g_objects); }
    ~SendCommand() { InterlockedDecrement(&g_objects); }

    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IExplorerCommand) {
            *ppv = static_cast<IExplorerCommand *>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&_refs); }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        LONG r = InterlockedDecrement(&_refs);
        if (!r) delete this;
        return r;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray *, LPWSTR *name) override
    {
        return SHStrDupW(chinese() ? L"用 AetherDrop 发送" : L"Send with AetherDrop", name);
    }
    IFACEMETHODIMP GetIcon(IShellItemArray *, LPWSTR *icon) override
    {
        return SHStrDupW((exe() + L",0").c_str(), icon);
    }
    IFACEMETHODIMP GetToolTip(IShellItemArray *, LPWSTR *tip) override
    {
        *tip = nullptr;
        return E_NOTIMPL;
    }
    IFACEMETHODIMP GetCanonicalName(GUID *guid) override
    {
        *guid = CLSID_NekoSend;
        return S_OK;
    }
    IFACEMETHODIMP GetState(IShellItemArray *items, BOOL, EXPCMDSTATE *state) override
    {
        // Only things on disk can be sent (not Control Panel items and the like).
        *state = ECS_ENABLED;
        if (items) {
            SFGAOF attrs = 0;
            if (SUCCEEDED(items->GetAttributes(SIATTRIBFLAGS_AND, SFGAO_FILESYSTEM, &attrs)) &&
                !(attrs & SFGAO_FILESYSTEM))
                *state = ECS_HIDDEN;
        }
        return S_OK;
    }
    IFACEMETHODIMP Invoke(IShellItemArray *items, IBindCtx *) override
    {
        if (!items) return E_INVALIDARG;
        DWORD count = 0;
        if (FAILED(items->GetCount(&count))) return E_FAIL;
        std::wstring cmd;
        quote(cmd, exe());
        cmd += L" --send";
        for (DWORD i = 0; i < count; i++) {
            IShellItem *item = nullptr;
            if (FAILED(items->GetItemAt(i, &item))) continue;
            LPWSTR path = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) {
                quote(cmd, path);
                CoTaskMemFree(path);
            }
            item->Release();
        }
        STARTUPINFOW si = {sizeof si};
        PROCESS_INFORMATION pi = {};
        std::wstring app = exe();
        if (!CreateProcessW(app.c_str(), &cmd[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi))
            return HRESULT_FROM_WIN32(GetLastError());
        // The client brings its own window forward; let it.
        AllowSetForegroundWindow(pi.dwProcessId);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return S_OK;
    }
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS *flags) override
    {
        *flags = ECF_DEFAULT;
        return S_OK;
    }
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand **e) override
    {
        *e = nullptr;
        return E_NOTIMPL;
    }
};

class Factory final : public IClassFactory {
public:
    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) override
    {
        if (!ppv) return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory) {
            *ppv = static_cast<IClassFactory *>(this);
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return 2; }
    IFACEMETHODIMP_(ULONG) Release() override { return 1; }
    IFACEMETHODIMP CreateInstance(IUnknown *outer, REFIID riid, void **ppv) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto *cmd = new (std::nothrow) SendCommand();
        if (!cmd) return E_OUTOFMEMORY;
        HRESULT hr = cmd->QueryInterface(riid, ppv);
        cmd->Release();
        return hr;
    }
    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) InterlockedIncrement(&g_objects);
        else InterlockedDecrement(&g_objects);
        return S_OK;
    }
};

static Factory g_factory;

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void **ppv)
{
    if (clsid != CLSID_NekoSend) {
        *ppv = nullptr;
        return CLASS_E_CLASSNOTAVAILABLE;
    }
    return g_factory.QueryInterface(riid, ppv);
}

STDAPI DllCanUnloadNow() { return g_objects ? S_FALSE : S_OK; }
