using TabletDriverCleanup.Services;

namespace TabletDriverCleanup.Modules;

public interface IObjectToUninstall
{
    bool Matches(RegexCache regexCache, object obj);
}
