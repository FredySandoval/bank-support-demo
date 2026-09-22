"use strict";

const transferForm = document.getElementById("transfer-form");
if (transferForm) {
    transferForm.addEventListener("submit", (event) => {
        const source = document.getElementById("SourceAccountId").value;
        const destination = document.getElementById("DestinationAccountId").value;
        const amount = Number(document.getElementById("Amount").value);
        let error = "";
        if (!Number.isFinite(amount) || amount <= 0) {
            error = "Amount must be greater than zero.";
        } else if (source === destination) {
            error = "Source and destination must be different.";
        }
        document.getElementById("transfer-error").textContent = error;
        if (error) event.preventDefault();
    });
}
