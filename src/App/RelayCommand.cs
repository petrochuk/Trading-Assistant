using System.Windows.Input;

namespace TradingAssistant;

public class RelayCommand<T> : ICommand
{
    #region Fields

    readonly Action<T> _execute;
    readonly Predicate<T?>? _canExecute = null;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="DelegateCommand{T}"/>.
    /// </summary>
    /// <param name="execute">Delegate to execute when Execute is called on the command.  This can be null to just hook up a CanExecute delegate.</param>
    /// <remarks><seealso cref="CanExecute"/> will always return true.</remarks>
    public RelayCommand(Action<T> execute)
        : this(execute, null) {
    }

    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<T> execute, Predicate<T?>? canExecute) {
        if (execute == null)
            throw new ArgumentNullException("execute");

        _execute = execute;
        _canExecute = canExecute;
    }

    #endregion

    #region ICommand Members

    public event EventHandler? CanExecuteChanged;

    ///<summary>
    ///Defines the method that determines whether the command can execute in its current state.
    ///</summary>
    ///<param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
    ///<returns>
    ///true if this command can be executed; otherwise, false.
    ///</returns>
    public bool CanExecute(object? parameter) {
        return _canExecute == null ? true : _canExecute((T?)parameter);
    }

    public void RaiseCanExecuteChanged() {
        if (CanExecuteChanged != null)
            CanExecuteChanged(this, EventArgs.Empty);
    }

    ///<summary>
    /// Defines the method to be called when the command is invoked.
    ///</summary>
    ///<param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to <see langword="null" />.</param>
    public void Execute(object? parameter) {
        if (parameter == null) {
            throw new ArgumentException("Parameter cannot be null for value type command.", nameof(parameter));
        }
        _execute((T)parameter);
    }

    #endregion
}