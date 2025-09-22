(function ($) {
  $(function () {
    const $form = $('#formLead');
    const $btn = $('#btnSubmit');

    // CEP auto-fill (igual ao anterior)
    let lastCep = '', timer = null;

    function setBusy(b){
      $btn.data('busy', !!b);
      refreshSubmitState();
    }

    function fillFromViaCep(cep){
      if (!cep || cep.length !== 8 || cep === lastCep) return;
      lastCep = cep;
      setBusy(true);

      $.ajax({
        url: 'https://viacep.com.br/ws/'+encodeURIComponent(cep)+'/json/',
        dataType: 'json',
        timeout: 7000
      }).done(function(d){
        if(!d || d.erro){
          $('#Cidade').val('');
          $('#Uf').val('');
          $('#Bairro').val('');
          $('#Logradouro').val('');
          return;
        }
        $('#Cidade').val(d.localidade||'');
        $('#Uf').val(d.uf||'');
        $('#Bairro').val(d.bairro||'');
        $('#Logradouro').val((d.logradouro||'')+(d.complemento?' '+d.complemento:''));        
      }).always(function(){ setBusy(false); });
    }

    $('#Cep').on('input', function(){
      const cep = ($(this).val()||'').replace(/\D/g,'').slice(0,8);
      $(this).val(cep);
      clearTimeout(timer);
      if (cep.length === 8) {
        timer = setTimeout(function(){ fillFromViaCep(cep); }, 250);
      }
      refreshSubmitState();
    });

    $('#Cep').on('blur', function(){
      const cep = ($(this).val()||'').replace(/\D/g,'').slice(0,8);
      $(this).val(cep);
      if (cep.length === 8) fillFromViaCep(cep);
      refreshSubmitState();
    });

    // ---- Telefone: campo visível #TelefoneView -> hidden #Telefone com 55
    const $telView = $('#TelefoneView');
    const $telHidden = $('#Telefone');

    function normalizeTelView(){
      const digits = ($telView.val()||'').replace(/\D/g,'').slice(0,11);
      $telView.val(digits);
      let tel = digits;
      if (tel.length >= 10 && tel.length <= 11 && !tel.startsWith('55')) tel = '55' + tel;
      $telHidden.val(tel);
    }

    $telView.on('input', function(){ normalizeTelView(); refreshSubmitState(); });
    normalizeTelView();

    // ---- Habilita "Enviar" só quando obrigatórios estiverem OK
    function isEmail(v){
      v = (v||'').trim();
      if (!v) return false;
      return /^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/.test(v);
    }

    function refreshSubmitState(){
      const busy = $btn.data('busy') === true;
      const nome = ($('#Nome').val()||'').trim();
      const emailOk = isEmail($('#Email').val());
      const cepOk = ($('#Cep').val()||'').replace(/\D/g,'').length === 8;
      const tv = ($telView.val()||'').replace(/\D/g,'');
      const telOk = tv.length === 10 || tv.length === 11;
      const ok = !busy && nome.length >= 2 && emailOk && cepOk && telOk;
      $btn.prop('disabled', !ok);
    }

    $('#Nome, #Email, #Servico').on('input change', refreshSubmitState);
    refreshSubmitState();

    // ---- Modal de Termos
    const $modal = $('#termsModal');
    const $feedback = $('#termsFeedback');
    let consentGiven = false;

    function openModal(){
      $feedback.text('').removeClass('ok err');
      $modal.removeClass('hidden');
      $('body').addClass('no-scroll');
      $('#btnConcordo').trigger('focus');
    }
    function closeModal(){
      $modal.addClass('hidden');
      $('body').removeClass('no-scroll');
    }

    $('#termsClose').on('click', function(){ closeModal(); });

    $('#btnRecusar').on('click', function(){
      consentGiven = false;
      $feedback.text('É necessário aceitar os termos para continuar.').removeClass('ok').addClass('err');
    });

    $('#btnConcordo').on('click', function(){
      consentGiven = true;
      $('#AceiteContatoWhatsapp').val('true');
      $('#AceiteCompartilhamento').val('true');
      $('#AceiteUso').val('true');

      $feedback.text('Termos aceitos.').removeClass('err').addClass('ok');

      normalizeTelView();
      closeModal();
      $form.off('submit.terms').trigger('submit');
    });

    // Intercepta submit para abrir a modal antes
    $form.on('submit.terms', function(e){
      if (consentGiven === true) return true;
      e.preventDefault();
      openModal();
      return false;
    });

    // Sanitização final antes de enviar (após consentir)
    $form.on('submit', function(){
      $('#Cep').val(($('#Cep').val()||'').replace(/\D/g,'').slice(0,8));
      normalizeTelView();
    });
  });
})(jQuery);
