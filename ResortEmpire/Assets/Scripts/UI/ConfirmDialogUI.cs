using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Simple yes/no dialog on its own canvas. Create it with ConfirmDialogUI.Show; it removes itself
// when either button is pressed.
[DisallowMultipleComponent]
public sealed class ConfirmDialogUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelLabel;

    private Action onConfirm;
    private Action onCancel;

    public static ConfirmDialogUI Show(ConfirmDialogUI prefab, string title, string message,
        string confirmText, string cancelText, Action onConfirm, Action onCancel = null)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        ConfirmDialogUI dialog = Instantiate(prefab);
        dialog.Setup(title, message, confirmText, cancelText, onConfirm, onCancel);
        return dialog;
    }

    private void Setup(string title, string message, string confirmText, string cancelText,
        Action confirm, Action cancel)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        if (confirmLabel != null) confirmLabel.text = confirmText;
        if (cancelLabel != null) cancelLabel.text = cancelText;
        onConfirm = confirm;
        onCancel = cancel;
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    private void Confirm() => Close(onConfirm);

    private void Cancel() => Close(onCancel);

    private void Close(Action callback)
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
        onConfirm = null;
        onCancel = null;
        Destroy(gameObject);
        callback?.Invoke();
    }
}
